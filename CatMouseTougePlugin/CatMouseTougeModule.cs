using AssettoServer.Server.Plugin;
using Autofac;

namespace CatMouseTougePlugin;

public class CatMouseTougeModule : AssettoServerModule<CatMouseTougeConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<CatMouseTouge>().AsSelf().As<IAssettoServerAutostart>().SingleInstance();
        builder.RegisterType<EntryCarTougeSession>().AsSelf();
        builder.RegisterType<TougeSession>().AsSelf();
        builder.RegisterType<Race>().AsSelf();
    }

}
