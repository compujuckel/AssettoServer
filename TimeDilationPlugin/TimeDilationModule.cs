using AssettoServer.Server.Plugin;
using Autofac;

namespace TimeDilationPlugin;

public class TimeDilationModule : AssettoServerModule<TimeDilationConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<TimeDilationPlugin>().AsSelf().As<IAssettoServerAutostart>().SingleInstance();
    }
}
