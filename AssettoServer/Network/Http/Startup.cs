using System;
using System.Net.Http;
using AssettoServer.Hub.Contracts;
using AssettoServer.Server;
using AssettoServer.Server.Plugin;
using Grpc.Core;
using Grpc.Net.Client.Configuration;
using Grpc.Net.ClientFactory;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProtoBuf.Grpc.ClientFactory;

namespace AssettoServer.Network.Http
{
    public class Startup
    {
        private readonly ACServer _server;
        
        public Startup(ACServer acServer)
        {
            _server = acServer;
        }
        
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(_server);
            services.AddMetrics(_server.Metrics);
            services.AddAppMetricsCollectors();
            services.AddMetricsEndpoints();
            services.AddControllers().AddNewtonsoftJson();
            services.AddControllers(options =>
            {
                options.OutputFormatters.Add(new LuaOutputFormatter());
            });
            
            if (_server.GrpcChannelUri != null && _server.Configuration.Extra.HubConnection != null)
            {
                void grpcOptions(GrpcClientFactoryOptions options)
                {
                    options.Address = _server.GrpcChannelUri;
                    options.ChannelOptionsActions.Add(o =>
                    {
                        o.ServiceConfig = new ServiceConfig
                        {
                            MethodConfigs =
                            {
                                new MethodConfig
                                {
                                    Names = { MethodName.Default },
                                    RetryPolicy = new RetryPolicy
                                    {
                                        MaxAttempts = 5,
                                        InitialBackoff = TimeSpan.FromSeconds(1),
                                        MaxBackoff = TimeSpan.FromSeconds(10),
                                        BackoffMultiplier = 1.5,
                                        RetryableStatusCodes = { StatusCode.Unavailable }
                                    }
                                }
                            }
                        };
                        o.HttpHandler = new SocketsHttpHandler { EnableMultipleHttp2Connections = true };
                    });
                }

                services.AddScoped<AuthInterceptor>();
                services.AddCodeFirstGrpcClient<IPlayerClient>(grpcOptions).AddInterceptor<AuthInterceptor>();
                services.AddCodeFirstGrpcClient<IRaceChallengeLeaderboardClient>(grpcOptions).AddInterceptor<AuthInterceptor>();
            }

            var mvcBuilder = services.AddControllers();
            foreach (var plugin in _server.PluginLoader.LoadedPlugins)
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