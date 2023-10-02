using AssettoServer.Server.Plugin;
using Autofac;

namespace VotingTrackPlugin;

public class VotingTrackModule : AssettoServerModule<VotingTrackConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<VotingTrack>().AsSelf().As<IAssettoServerAutostart>().SingleInstance();
    }
}
