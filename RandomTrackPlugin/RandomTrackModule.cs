using AssettoServer.Server.Plugin;
using Autofac;

namespace RandomTrackPlugin;

public class RandomTrackModule : AssettoServerModule<RandomTrackConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<RandomTrack>().AsSelf().As<IAssettoServerAutostart>().SingleInstance();
    }
}
