using AssettoServer.Server.Configuration;
using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace AutoModerationPlugin;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class AutoModerationConfiguration : IValidateConfiguration<AutoModerationConfigurationValidator>
{
    [YamlMember(Description = "Kick players driving the wrong way. AI has to enabled for this to work")]
    public WrongWayKickConfiguration WrongWayKick { get; init; } = new();
    [YamlMember(Description = "Kick players driving without lights during the night")]
    public NoLightsKickConfiguration NoLightsKick { get; init; } = new();
    [YamlMember(Description = "Kick players blocking the road. AI has to be enabled for this to work")]
    public BlockingRoadKickConfiguration BlockingRoadKick { get; init; } = new();
}

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class WrongWayKickConfiguration
{
    [YamlMember(Description = "Set to true to enable")]
    public bool Enabled { get; set; } = false;
    [YamlMember(Description = "Time after the player gets kicked. A warning will be sent in chat after half this time")]
    public int DurationSeconds { get; set; } = 20;
    [YamlMember(Description = "Players driving slower than this speed will not be kicked")]
    public int MinimumSpeedKph { get; set; } = 20;

    [YamlIgnore] public float MinimumSpeedMs => MinimumSpeedKph / 3.6f;
}

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class NoLightsKickConfiguration
{
    [YamlMember(Description = "Set to true to enable")]
    public bool Enabled { get; set; } = false;
    [YamlMember(Description = "Time after the player gets kicked. A warning will be sent in chat after half this time")]
    public int DurationSeconds { get; set; } = 60;
    [YamlMember(Description = "Players driving slower than this speed will not be kicked")]
    public int MinimumSpeedKph { get; set; } = 20;
    
    [YamlIgnore] public float MinimumSpeedMs => MinimumSpeedKph / 3.6f;
}

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class BlockingRoadKickConfiguration
{
    [YamlMember(Description = "Set to true to enable")]
    public bool Enabled { get; set; } = false;
    [YamlMember(Description = "Time after the player gets kicked. A warning will be sent in chat after half this time")]
    public int DurationSeconds { get; set; } = 30;
    [YamlMember(Description = "Players driving faster than this speed will not be kicked")]
    public int MaximumSpeedKph { get; set; } = 5;
    
    [YamlIgnore] public float MaximumSpeedMs => MaximumSpeedKph / 3.6f;
}
