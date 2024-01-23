using AssettoServer.Server.Plugin;
using Autofac;
using VotingPresetPlugin.Preset;

namespace VotingPresetPlugin;

public class VotingPresetModule : AssettoServerModule<VotingPresetConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<PresetConfigurationManager>().AsSelf().SingleInstance();
        builder.RegisterType<PresetManager>().AsSelf().SingleInstance();
        
        builder.RegisterType<VotingPresetPlugin>().AsSelf().As<IAssettoServerAutostart>().SingleInstance();
    }
}
