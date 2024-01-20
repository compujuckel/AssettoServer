using AssettoServer.Server.Configuration;
using Autofac;
using CyclePresetPlugin.Preset.Restart;

namespace CyclePresetPlugin.Preset;

public class PresetModule : Module
{
    private readonly ACServerConfiguration _configuration;

    public PresetModule(ACServerConfiguration configuration)
    {
        _configuration = configuration;
    }

    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<PresetConfigurationManager>().AsSelf().SingleInstance();
        builder.RegisterType<PresetImplementation>().AsSelf().SingleInstance();
        builder.RegisterType<PresetManager>().AsSelf().SingleInstance();
        
        builder.RegisterType<RestartImplementation>().AsSelf().SingleInstance();
    }
}
