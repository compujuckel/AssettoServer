using AssettoServer.Server.Plugin;
using Autofac;

namespace RandomWeatherPlugin;

public class RandomWeatherModule : AssettoServerModule<RandomWeatherConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<RandomWeather>().AsSelf().As<IAssettoServerAutostart>().SingleInstance();
    }
}
