using AssettoServer.Server.Plugin;
using Autofac;

namespace ReplayPlugin;

public class ReplayModule : AssettoServerModule
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<ReplayPlugin>().AsSelf().As<IAssettoServerAutostart>().SingleInstance();
        builder.RegisterType<ReplayManager>().AsSelf().SingleInstance();
    }
}
