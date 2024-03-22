using AssettoServer.Server.Plugin;
using Autofac;

namespace CustomCommandPlugin;

public class CustomCommandModule : AssettoServerModule<CustomCommandConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<CustomCommand>().AsSelf().As<IAssettoServerAutostart>().SingleInstance();
    }
}
