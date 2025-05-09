using AssettoServer.Server.Plugin;
using Autofac;

namespace TougePlugin;

public class TougeModule : AssettoServerModule<TougeConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<Touge>().AsSelf().As<IAssettoServerAutostart>().SingleInstance();
        builder.RegisterType<EntryCarTougeSession>().AsSelf();
        builder.RegisterType<TougeSession>().AsSelf();
        builder.RegisterType<Race>().AsSelf();
    }

}
