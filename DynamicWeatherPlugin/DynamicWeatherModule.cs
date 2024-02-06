using AssettoServer.Server.Plugin;
using Autofac;

namespace DynamicWeatherPlugin;

public class DynamicWeatherModule : AssettoServerModule<DynamicWeatherConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<DynamicWeather>().AsSelf().As<IAssettoServerAutostart>().SingleInstance();
    }
}
