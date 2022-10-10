using AssettoServer.Server.Plugin;
using Autofac;

namespace RandomDynamicTrafficPlugin
{
    public class RandomDynamicTrafficModule : AssettoServerModule<RandomDynamicTrafficConfiguration>
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<RandomDynamicTraffic>().AsSelf().As<IAssettoServerAutostart>().SingleInstance();
        }
    }
}