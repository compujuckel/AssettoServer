using AssettoServer.Server.Plugin;
using Autofac;
using Microsoft.Extensions.Hosting;

namespace RaceChallengePlugin;

public class RaceChallengeModule : AssettoServerModule
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<RaceChallengePlugin>().AsSelf().As<IHostedService>().SingleInstance();
        builder.RegisterType<EntryCarRace>().AsSelf();
        builder.RegisterType<Race>().AsSelf();
    }
}
