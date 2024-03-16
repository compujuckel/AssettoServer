using System;
using System.Net.Http;
using AssettoServer.Commands;
using AssettoServer.Commands.Contexts;
using AssettoServer.Commands.TypeParsers;
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
using Microsoft.Extensions.Hosting;
using Prometheus;
using Qmmands;

namespace AssettoServer.Network.Http;

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
        builder.RegisterModule(new WeatherModule(_configuration));
        builder.RegisterModule(new AiModule(_configuration));
        builder.RegisterType<HttpClient>().AsSelf();
        builder.RegisterType<ACTcpClient>().AsSelf();
        builder.RegisterType<EntryCar>().AsSelf();
        builder.RegisterType<ChatCommandContext>().AsSelf();
        builder.RegisterType<RconCommandContext>().AsSelf();
        builder.RegisterType<SessionState>().AsSelf();
        builder.RegisterType<ACClientTypeParser>().AsSelf();
        builder.RegisterType<ChatService>().AsSelf().SingleInstance().AutoActivate();
        builder.RegisterType<CSPFeatureManager>().AsSelf().SingleInstance();
        builder.RegisterType<KunosLobbyRegistration>().AsSelf().SingleInstance();
        builder.RegisterType<UserGroupManager>().AsSelf().SingleInstance();
        builder.RegisterType<FileBasedUserGroup>().AsSelf();
        builder.RegisterType<FileBasedUserGroupProvider>().AsSelf().As<IUserGroupProvider>().As<IHostedService>().SingleInstance();
        builder.RegisterType<AdminService>().As<IAdminService>().SingleInstance();
        builder.RegisterType<BlacklistService>().As<IBlacklistService>().SingleInstance();
        builder.RegisterType<WhitelistService>().As<IWhitelistService>().SingleInstance();
        builder.RegisterType<IniTrackParamsProvider>().As<ITrackParamsProvider>().SingleInstance();
        builder.RegisterType<CSPServerScriptProvider>().AsSelf().SingleInstance();
        builder.RegisterType<CSPClientMessageTypeManager>().AsSelf().SingleInstance();
        builder.RegisterType<CSPClientMessageHandler>().AsSelf().SingleInstance();
        builder.RegisterType<SessionManager>().AsSelf().SingleInstance();
        builder.RegisterType<VoteManager>().AsSelf().SingleInstance();
        builder.RegisterType<EntryCarManager>().AsSelf().SingleInstance();
        builder.RegisterType<IpApiGeoParamsProvider>().As<IGeoParamsProvider>();
        builder.RegisterType<GeoParamsManager>().AsSelf().SingleInstance();
        builder.RegisterType<ChecksumManager>().AsSelf().SingleInstance();
        builder.RegisterType<CSPServerExtraOptions>().AsSelf().SingleInstance();
        builder.RegisterType<ACTcpServer>().AsSelf().SingleInstance(); // Not registered as IHostedService, this is hardcoded to start first
        builder.RegisterType<ACUdpServer>().AsSelf().SingleInstance(); // Not registered as IHostedService, this is hardcoded to start first
        builder.RegisterType<ACServer>().AsSelf().As<IHostedService>().SingleInstance();
        builder.RegisterType<OpenSlotFilterChain>().AsSelf().SingleInstance();
        builder.RegisterType<WhitelistSlotFilter>().As<IOpenSlotFilter>();
        builder.RegisterType<GuidSlotFilter>().As<IOpenSlotFilter>();
        builder.RegisterType<SignalHandler>().AsSelf().As<IHostedService>().SingleInstance();
        builder.RegisterType<UpnpService>().AsSelf().As<IAssettoServerAutostart>().SingleInstance();
        builder.RegisterType<ConfigurationSerializer>().AsSelf();
        builder.RegisterType<ACClientAuthentication>().AsSelf().SingleInstance().AutoActivate();
        builder.RegisterType<HttpInfoCache>().AsSelf().As<IAssettoServerAutostart>().SingleInstance();
        builder.RegisterType<DefaultCMContentProvider>().As<ICMContentProvider>().SingleInstance();
        builder.RegisterType<CommandService>().AsSelf().SingleInstance();

        if (_configuration.Extra.EnableLegacyPluginInterface)
        {
            builder.RegisterType<UdpPluginServer>().AsSelf().As<IAssettoServerAutostart>().SingleInstance();
        }
            
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

        if (_configuration.Extra.RconPort != 0)
        {
            builder.RegisterType<RconClient>().AsSelf();
            builder.RegisterType<RconServer>().AsSelf().As<IHostedService>().SingleInstance();
        }

        foreach (var plugin in _loader.LoadedPlugins)
        {
            if (plugin.ConfigurationType != null) builder.RegisterType(plugin.ConfigurationType).AsSelf();
            builder.RegisterModule(plugin.Instance);
        }

        _configuration.LoadPluginConfiguration(_loader, builder);
    }
    
    // This method gets called by the runtime. Use this method to add services to the container.
    // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy(name: "ServerQueryPolicy",
                policy =>
                {
                    policy.WithOrigins(_configuration.Extra.CorsAllowedOrigins?.ToArray() ?? Array.Empty<string>());
                });
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

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapMetrics();
            endpoints.MapControllers();
        });
        
        foreach (var plugin in _loader.LoadedPlugins)
        {
            plugin.Instance.Configure(app, env);
        }
    }
}
