using AssettoServer.Server.Configuration;
using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace AutoModerationPlugin;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class AutoModerationConfiguration : IValidateConfiguration<AutoModerationConfigurationValidator>
{
    public WrongWayKickConfiguration WrongWayKick { get; init; } = new();
    public NoLightsKickConfiguration NoLightsKick { get; init; } = new();
    public BlockingRoadKickConfiguration BlockingRoadKick { get; init; } = new();
}

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class WrongWayKickConfiguration
{
    public bool Enabled { get; set; } = false;
    public int DurationSeconds { get; set; } = 20;
    public int MinimumSpeedKph { get; set; } = 20;

    [YamlIgnore] public float MinimumSpeedMs => MinimumSpeedKph / 3.6f;
}

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class NoLightsKickConfiguration
{
    public bool Enabled { get; set; } = false;
    public int DurationSeconds { get; set; } = 60;
    public int MinimumSpeedKph { get; set; } = 20;
    
    [YamlIgnore] public float MinimumSpeedMs => MinimumSpeedKph / 3.6f;
}

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class BlockingRoadKickConfiguration
{
    public bool Enabled { get; set; } = false;
    public int DurationSeconds { get; set; } = 30;
    public int MaximumSpeedKph { get; set; } = 5;
    
    [YamlIgnore] public float MaximumSpeedMs => MaximumSpeedKph / 3.6f;
}
