using AssettoServer.Server;
using AssettoServer.Server.Plugin;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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