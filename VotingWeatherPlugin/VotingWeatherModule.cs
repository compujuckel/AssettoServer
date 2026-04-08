using AssettoServer.Server.Plugin;
using Autofac;
using Microsoft.Extensions.Hosting;

namespace VotingWeatherPlugin;

public class VotingWeatherModule : AssettoServerModule<VotingWeatherConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<VotingWeather>().AsSelf().As<IHostedService>().SingleInstance();
    }
}
