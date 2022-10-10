using AssettoServer.Server.Plugin;
using Autofac;

namespace GroupStreetRacingPlugin
{
    public class GroupStreetRacingModule : AssettoServerModule<GroupStreetRacingConfiguration>
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<GroupStreetRacing>().AsSelf().As<IAssettoServerAutostart>().SingleInstance();
        }
    }
}