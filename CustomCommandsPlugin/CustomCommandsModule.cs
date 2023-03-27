using AssettoServer.Server.Plugin;
using Autofac;

namespace CustomCommandsPlugin;

public class CustomCommandsModule : AssettoServerModule<CustomCommandsConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<CustomCommands>().AsSelf().As<IAssettoServerAutostart>().SingleInstance();
    }
}
