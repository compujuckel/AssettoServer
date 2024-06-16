using AssettoServer.Server.Plugin;
using Autofac;

namespace FastTravelPlugin;

public class FastTravelModule : AssettoServerModule
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<FastTravelPlugin>().AsSelf().As<IAssettoServerAutostart>().SingleInstance();
    }
}
