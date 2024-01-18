using AssettoServer.Server.Plugin;
using Autofac;
using Microsoft.Extensions.DependencyInjection;

namespace CyclePresetPlugin;

public class CyclePresetModule : AssettoServerModule<CyclePresetConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<CyclePresetPlugin>().AsSelf().As<IAssettoServerAutostart>().SingleInstance();
    }
}
