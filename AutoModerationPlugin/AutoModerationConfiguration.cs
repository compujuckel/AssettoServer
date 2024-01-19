using AssettoServer.Server.Configuration;
using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace AutoModerationPlugin;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class AutoModerationConfiguration : IValidateConfiguration<AutoModerationConfigurationValidator>
{
    [YamlMember(Description = "Kick players driving the wrong way. AI has to enabled for this to work")]
    public WrongWayPenaltyConfiguration WrongWayPenalty { get; init; } = new();
    [YamlMember(Description = "Kick players driving without lights during the night")]
    public NoLightsPenaltyConfiguration NoLightsPenalty { get; init; } = new();
    [YamlMember(Description = "Kick players blocking the road. AI has to be enabled for this to work")]
    public BlockingRoadPenaltyConfiguration BlockingRoadPenalty { get; init; } = new();
}

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class WrongWayPenaltyConfiguration
{
    [YamlMember(Description = "Set to true to enable")]
    public bool Enabled { get; set; } = false;
    [YamlMember(Description = "Time after the player gets kicked. A warning will be sent in chat after half this time")]
    public int DurationSeconds { get; set; } = 20;
    [YamlMember(Description = "Players driving slower than this speed will not be kicked")]
    public int MinimumSpeedKph { get; set; } = 20;
    [YamlMember(Description = "The amount of times a player will be send to pits before being kicked")]
    public int PitsBeforeKick { get; set; } = 3;

    [YamlIgnore] public float MinimumSpeedMs => MinimumSpeedKph / 3.6f;
}

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class NoLightsPenaltyConfiguration
{
    [YamlMember(Description = "Set to true to enable")]
    public bool Enabled { get; set; } = false;
    [YamlMember(Description = "Time in which no warning or signs will be sent")]
    public int IgnoreSeconds { get; set; } = 2;
    [YamlMember(Description = "Time after the player gets kicked. A warning will be sent in chat after half this time")]
    public int DurationSeconds { get; set; } = 60;
    [YamlMember(Description = "Players driving slower than this speed will not be kicked")]
    public int MinimumSpeedKph { get; set; } = 20;
    [YamlMember(Description = "The amount of times a player will be send to pits before being kicked")]
    public int PitsBeforeKick { get; set; } = 3;
    
    [YamlIgnore] public float MinimumSpeedMs => MinimumSpeedKph / 3.6f;
}

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class BlockingRoadPenaltyConfiguration
{
    [YamlMember(Description = "Set to true to enable")]
    public bool Enabled { get; set; } = false;
    [YamlMember(Description = "Time after the player gets kicked. A warning will be sent in chat after half this time")]
    public int DurationSeconds { get; set; } = 30;
    [YamlMember(Description = "Players driving faster than this speed will not be kicked")]
    public int MaximumSpeedKph { get; set; } = 5;
    [YamlMember(Description = "The amount of times a player will be send to pits before being kicked")]
    public int PitsBeforeKick { get; set; } = 3;
    
    [YamlIgnore] public float MaximumSpeedMs => MaximumSpeedKph / 3.6f;
}
