using AssettoServer.Server.Plugin;
using Autofac;

namespace VotingWeatherPlugin;

public class VotingWeatherModule : AssettoServerModule<VotingWeatherConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<VotingWeather>().AsSelf().As<IAssettoServerAutostart>().SingleInstance();
    }
}
