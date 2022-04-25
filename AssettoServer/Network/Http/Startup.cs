using System.Net.Http;
using App.Metrics;
using AssettoServer.Commands;
using AssettoServer.Commands.TypeParsers;
using AssettoServer.Network.Tcp;
using AssettoServer.Network.Udp;
using AssettoServer.Server;
using AssettoServer.Server.Ai;
using AssettoServer.Server.Blacklist;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.GeoParams;
using AssettoServer.Server.Plugin;
using AssettoServer.Server.TrackParams;
using AssettoServer.Server.Weather;
using Autofac;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AssettoServer.Network.Http
{
    public class Startup
    {
        private readonly ACServerConfiguration _configuration;
        private readonly ACPluginLoader _loader;
        
        public Startup(ACServerConfiguration configuration, ACPluginLoader loader)
        {
            _configuration = configuration;
            _loader = loader;
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
            builder.RegisterType<ACCommandContext>().AsSelf();
            builder.RegisterType<SessionState>().AsSelf();
            builder.RegisterType<ACClientTypeParser>().AsSelf();
            builder.RegisterType<ChatService>().AsSelf().SingleInstance();
            builder.RegisterType<KunosLobbyRegistration>().AsSelf().SingleInstance().AutoActivate();
            builder.RegisterType<DefaultBlacklistService>().As<IBlacklistService>().SingleInstance();
            builder.RegisterType<IniTrackParamsProvider>().As<ITrackParamsProvider>().SingleInstance();
            builder.RegisterType<CSPServerScriptProvider>().AsSelf().SingleInstance();
            builder.RegisterType<Steam>().As<IHostedService>().AsSelf().SingleInstance();
            builder.RegisterType<SessionManager>().AsSelf().SingleInstance();
            builder.RegisterType<EntryCarManager>().AsSelf().SingleInstance();
            builder.RegisterType<IpApiGeoParamsProvider>().As<IGeoParamsProvider>();
            builder.RegisterType<GeoParamsManager>().AsSelf().SingleInstance();
            builder.RegisterType<ACUdpServer>().AsSelf().As<IHostedService>().SingleInstance();
            builder.RegisterType<ACTcpServer>().AsSelf().As<IHostedService>().SingleInstance();
            builder.RegisterType<ACServer>().AsSelf().As<IHostedService>().SingleInstance();
        }
        
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMetrics(new MetricsBuilder()
                .Configuration.Configure(options => { options.DefaultContextLabel = "AssettoServer"; })
                .OutputMetrics.AsPrometheusPlainText()
                .Build());
            services.AddAppMetricsCollectors();
            services.AddMetricsEndpoints();
            services.AddControllers().AddNewtonsoftJson();
            services.AddControllers(options =>
            {
                options.OutputFormatters.Add(new LuaOutputFormatter());
            });

            var mvcBuilder = services.AddControllers();
            foreach (var plugin in _loader.LoadedPlugins)
            {
                // ReSharper disable once SuspiciousTypeConversion.Global
                if (plugin.Instance is IConfigureServices configureServices)
                {
                    configureServices.ConfigureServices(services);
                }
                
                mvcBuilder.AddApplicationPart(plugin.Assembly);
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
            app.UseMetricsEndpoint();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}