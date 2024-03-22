using AssettoServer.Server.Plugin;
using Autofac;

namespace TeleportPlugin;

public class TeleportModule : AssettoServerModule
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<TeleportPlugin>().AsSelf().AutoActivate().SingleInstance();
    }
}
