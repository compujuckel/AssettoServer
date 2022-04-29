using AssettoServer.Server.Plugin;
using Autofac;

namespace SamplePlugin;

public class SampleModule : AssettoServerModule<SampleConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<Sample>().AsSelf().As<IAssettoServerAutostart>().SingleInstance();
    }
}
