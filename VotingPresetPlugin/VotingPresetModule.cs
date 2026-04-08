using AssettoServer.Server.Plugin;
using Autofac;
using Microsoft.Extensions.Hosting;
using VotingPresetPlugin.Preset;

namespace VotingPresetPlugin;

public class VotingPresetModule : AssettoServerModule<VotingPresetConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<PresetConfigurationManager>().AsSelf().SingleInstance();
        builder.RegisterType<PresetManager>().AsSelf().SingleInstance();
        builder.RegisterType<VotingPresetPlugin>().AsSelf().As<IHostedService>().SingleInstance();
    }

    public override VotingPresetConfiguration ReferenceConfiguration => new()
    {
        EnableReconnect = true,
        EnableVote = false,
        EnableStayOnTrack = false,
        IntervalMinutes = 60,
        TransitionDelaySeconds = 30,
        TransitionDurationSeconds = 10,
        Meta = new()
        {
            Name = "SRP",
            AdminOnly = false
        }
    };
}
