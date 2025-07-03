using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using AssettoServer.Commands;
using AssettoServer.Commands.Contexts;
using AssettoServer.Commands.TypeParsers;
using AssettoServer.Network;
using AssettoServer.Network.Http;
using AssettoServer.Network.Http.Authentication;
using AssettoServer.Network.Rcon;
using AssettoServer.Network.Tcp;
using AssettoServer.Network.Udp;
using AssettoServer.Server;
using AssettoServer.Server.Admin;
using AssettoServer.Server.Ai;
using AssettoServer.Server.Blacklist;
using AssettoServer.Server.CMContentProviders;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Configuration.Serialization;
using AssettoServer.Server.GeoParams;
using AssettoServer.Server.OpenSlotFilters;
using AssettoServer.Server.Plugin;
using AssettoServer.Server.Steam;
using AssettoServer.Server.TrackParams;
using AssettoServer.Server.UserGroup;
using AssettoServer.Server.Weather;
using AssettoServer.Server.Whitelist;
using Autofac;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Prometheus;
using Qmmands;

namespace AssettoServer;

public class Startup
{
    private readonly ACServerConfiguration _configuration;
    private readonly ACPluginLoader _loader;

    public Startup(ACServerConfiguration configuration)
    {
        _configuration = configuration;
        _loader = new ACPluginLoader(configuration.LoadPluginsFromWorkdir);
    }

    [UsedImplicitly]
    public void ConfigureContainer(ContainerBuilder builder)
    {
        builder.RegisterInstance(_configuration);
        builder.RegisterInstance(_loader);
        
        // Registration order == order in which hosted services are started
        builder.RegisterType<ACServer>().AsSelf().As<IHostedService>().SingleInstance();
        builder.RegisterType<SessionManager>().AsSelf().As<IHostedService>().SingleInstance();
        builder.RegisterType<ACTcpServer>().AsSelf().As<IHostedService>().SingleInstance();
        builder.RegisterType<ACUdpServer>().AsSelf().As<IHostedService>().SingleInstance();
        builder.RegisterModule(new WeatherModule(_configuration));
        builder.RegisterModule(new AiModule(_configuration));
        builder.RegisterType<FileBasedUserGroupProvider>().AsSelf().As<IUserGroupProvider>().As<IHostedService>().SingleInstance();
        builder.RegisterType<SignalHandler>().AsSelf().As<IHostedService>().SingleInstance();
        builder.RegisterType<HttpInfoCache>().AsSelf().As<IHostedService>().SingleInstance();
        RegisterLegacyPluginInterface();
        RegisterSteam();
        RegisterRcon();
        
        foreach (var plugin in _loader.LoadedPlugins)
        {
            if (plugin.ConfigurationType != null) builder.RegisterType(plugin.ConfigurationType).AsSelf();
            builder.RegisterModule(plugin.Instance);
        }
        
        // Do this last so we don't register before a plugin fails to start
        builder.RegisterType<UpnpService>().AsSelf().As<IHostedService>().SingleInstance();
        builder.RegisterType<KunosLobbyRegistration>().AsSelf().As<IHostedService>().SingleInstance();
        
        // No hosted services below this line
        
        builder.RegisterType<HttpClient>().AsSelf();
        builder.RegisterType<ACTcpClient>().AsSelf();
        builder.RegisterType<EntryCar>().AsSelf();
        builder.RegisterType<ChatCommandContext>().AsSelf();
        builder.RegisterType<RconCommandContext>().AsSelf();
        builder.RegisterType<SessionState>().AsSelf();
        builder.RegisterType<ACClientTypeParser>().AsSelf();
        builder.RegisterType<ChatService>().AsSelf().SingleInstance().AutoActivate();
        builder.RegisterType<CSPFeatureManager>().AsSelf().SingleInstance();
        builder.RegisterType<UserGroupManager>().AsSelf().SingleInstance();
        builder.RegisterType<FileBasedUserGroup>().AsSelf();
        builder.RegisterType<AdminService>().As<IAdminService>().SingleInstance();
        builder.RegisterType<BlacklistService>().As<IBlacklistService>().SingleInstance();
        builder.RegisterType<WhitelistService>().As<IWhitelistService>().SingleInstance();
        builder.RegisterType<IniTrackParamsProvider>().As<ITrackParamsProvider>().SingleInstance();
        builder.RegisterType<CSPServerScriptProvider>().AsSelf().SingleInstance();
        builder.RegisterType<CSPClientMessageTypeManager>().AsSelf().SingleInstance();
        builder.RegisterType<CSPClientMessageHandler>().AsSelf().SingleInstance();
        builder.RegisterType<VoteManager>().AsSelf().SingleInstance();
        builder.RegisterType<EntryCarManager>().AsSelf().SingleInstance();
        builder.RegisterType<IpApiGeoParamsProvider>().As<IGeoParamsProvider>();
        builder.RegisterType<GeoParamsManager>().AsSelf().SingleInstance();
        builder.RegisterType<ChecksumManager>().AsSelf().SingleInstance();
        builder.RegisterType<CSPServerExtraOptions>().AsSelf().SingleInstance();
        builder.RegisterType<OpenSlotFilterChain>().AsSelf().SingleInstance();
        builder.RegisterType<WhitelistSlotFilter>().As<IOpenSlotFilter>();
        builder.RegisterType<GuidSlotFilter>().As<IOpenSlotFilter>();
        builder.RegisterType<ConfigurationSerializer>().AsSelf();
        builder.RegisterType<DefaultCMContentProvider>().As<ICMContentProvider>().SingleInstance();
        builder.RegisterType<CommandService>().AsSelf().SingleInstance();

        if (_configuration.GeneratePluginConfigs)
        {
            var loader = new ACPluginLoader(_configuration.LoadPluginsFromWorkdir);
            loader.LoadPlugins(loader.AvailablePlugins.Select(p => p.Key).ToList());
            _configuration.LoadPluginConfiguration(loader, null);
        }

        _configuration.LoadPluginConfiguration(_loader, builder);
        return;
        
        void RegisterLegacyPluginInterface()
        {
            if (_configuration.Extra.EnableLegacyPluginInterface)
            {
                builder.RegisterType<UdpPluginServer>().AsSelf().As<IHostedService>().SingleInstance();
            }
        }

        void RegisterSteam()
        {
            if (_configuration.Extra.UseSteamAuth)
            {
#if DISABLE_STEAM
                builder.RegisterType<WebApiSteam>().As<ISteam>().SingleInstance();
#else
                builder.RegisterType<NativeSteam>().As<IHostedService>().As<ISteam>().SingleInstance();
#endif
                builder.RegisterType<SteamManager>().AsSelf().SingleInstance().AutoActivate();
                builder.RegisterType<SteamSlotFilter>().As<IOpenSlotFilter>();
            }
        }

        void RegisterRcon()
        {
            if (_configuration.Extra.RconPort != 0)
            {
                builder.RegisterType<RconClient>().AsSelf();
                builder.RegisterType<RconServer>().AsSelf().As<IHostedService>().SingleInstance();
            }
        }
    }
    
    // This method gets called by the runtime. Use this method to add services to the container.
    // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
    public void ConfigureServices(IServiceCollection services)
    {
        services.Configure<HostOptions>(options =>
        {
            options.ShutdownTimeout = TimeSpan.FromSeconds(5);
            // This defaults to false, explicitly adding it here in case someone thinks it could be changed...
            // We can't due to dependencies between hosted services
            options.ServicesStartConcurrently = false;
            // Same for shutdown, otherwise we might not get the server shutdown chat message out
            options.ServicesStopConcurrently = false;
        });
        services.AddCors(options =>
        {
            options.AddPolicy(name: "ServerQueryPolicy", 
                policy => { policy.WithOrigins(_configuration.Extra.CorsAllowedOrigins?.ToArray() ?? []); });
        });
        services.AddAuthentication(o =>
            {
                o.DefaultScheme = "";
            })
            .AddScheme<ACClientAuthenticationSchemeOptions, ACClientAuthenticationHandler>(
                ACClientAuthenticationSchemeOptions.Scheme, _ => { });
        services.AddAuthorization();
        services.AddControllers().AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.TypeInfoResolverChain.Insert(0, JsonSourceGenerationContext.Default);
        });
        services.AddControllers(options =>
        {
            options.OutputFormatters.Add(new LuaOutputFormatter());
        });
        
        var mvcBuilder = services.AddControllers();

        if (_configuration.Extra.EnablePlugins != null)
        {
            _loader.LoadPlugins(_configuration.Extra.EnablePlugins);
            
            foreach (var plugin in _loader.LoadedPlugins)
            {
                plugin.Instance.ConfigureServices(services);
                mvcBuilder.AddApplicationPart(plugin.Assembly);
            }
        }
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseRouting();
        app.UseCors();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseStaticFiles(new StaticFileOptions
        {
            RequestPath = "/static",
            ServeUnknownFileTypes = true,
        });

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapMetrics();
            endpoints.MapControllers();
        });
        
        foreach (var plugin in _loader.LoadedPlugins)
        {
            var wwwrootPath = Path.Combine(plugin.Directory, "wwwroot");
            if (Directory.Exists(wwwrootPath))
            {
                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new PhysicalFileProvider(wwwrootPath),
                    RequestPath = $"/static/{plugin.Name}",
                    ServeUnknownFileTypes = true,
                });
            }
            
            plugin.Instance.Configure(app, env);
        }
    }
}
