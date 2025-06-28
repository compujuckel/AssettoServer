using AssettoServer.Server.Plugin;
using Autofac;
using Microsoft.Extensions.Hosting;

namespace LiveWeatherPlugin;

public class LiveWeatherModule : AssettoServerModule<LiveWeatherConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<LiveWeatherProvider>().AsSelf().As<IHostedService>().SingleInstance();
    }
}
