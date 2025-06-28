using AssettoServer.Server.Plugin;
using Autofac;

namespace FastTravelPlugin;

public class FastTravelModule : AssettoServerModule<FastTravelConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<FastTravelPlugin>().AsSelf().As<IAssettoServerAutostart>().SingleInstance();
    }

    public override FastTravelConfiguration ReferenceConfiguration => new()
    {
        MapZoomValues = [100, 200, 400, 600],
        MapMoveSpeeds = [1, 2, 3, 0],
        ShowMapImage = false,
        MapFixedTargetPosition = [0, 0, 0]
    };
}
