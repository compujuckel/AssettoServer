using AssettoServer.Server.Plugin;
using Autofac;
using CyclePresetPlugin.Preset;

namespace CyclePresetPlugin;

public class CyclePresetModule : AssettoServerModule<CyclePresetConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<PresetConfigurationManager>().AsSelf().SingleInstance();
        builder.RegisterType<PresetImplementation>().AsSelf().SingleInstance();
        builder.RegisterType<PresetManager>().AsSelf().SingleInstance();
        
        builder.RegisterType<CyclePresetPlugin>().AsSelf().As<IAssettoServerAutostart>().SingleInstance();
    }
}
