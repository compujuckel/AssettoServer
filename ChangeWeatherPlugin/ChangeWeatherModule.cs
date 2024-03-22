using AssettoServer.Server.Plugin;
using Autofac;

namespace ChangeWeatherPlugin;

public class ChangeWeatherModule : AssettoServerModule
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<ChangeWeatherPlugin>().AsSelf().AutoActivate().SingleInstance();
    }
}
