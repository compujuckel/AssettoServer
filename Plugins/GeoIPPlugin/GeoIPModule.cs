using AssettoServer.Server.Plugin;
using Autofac;

namespace GeoIPPlugin;

public class GeoIPModule : AssettoServerModule<GeoIPConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<GeoIP>().AsSelf().AutoActivate().SingleInstance();
    }
}
