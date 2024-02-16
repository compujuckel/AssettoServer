using AssettoServer.Server.Configuration;
using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace AutoModerationPlugin;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class AutoModerationConfiguration : IValidateConfiguration<AutoModerationConfigurationValidator>
{
    [YamlMember(Description = "Kick players that are AFK")]
    public AfkPenaltyConfiguration AfkPenalty { get; init; } = new();
    [YamlMember(Description = "Kick players with a high ping")]
    public HighPingPenaltyConfiguration HighPingPenalty { get; init; } = new();
    [YamlMember(Description = "Penalise players driving the wrong way. AI has to enabled for this to work")]
    public WrongWayPenaltyConfiguration WrongWayPenalty { get; init; } = new();
    [YamlMember(Description = "Penalise players driving without lights during the night")]
    public NoLightsPenaltyConfiguration NoLightsPenalty { get; init; } = new();
    [YamlMember(Description = "Penalise players blocking the road. AI has to be enabled for this to work")]
    public BlockingRoadPenaltyConfiguration BlockingRoadPenalty { get; init; } = new();
}

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class AfkPenaltyConfiguration
{
    [YamlMember(Description = "Set to true to enable")]
    public bool Enabled { get; set; } = true;
    [YamlMember(Description = "Don't kick if at least one open slot of the same car model is available")]
    public bool IgnoreWithOpenSlots { get; set; } = true;
    [YamlMember(Description = "Time after the player gets kicked. A warning will be sent in chat one minute before this time")]
    public int DurationMinutes { get; set; } = 10;
    [YamlMember(Description = "Set this to MinimumSpeed to not reset the AFK timer on chat messages / controller inputs and require players to actually drive")]
    public AfkPenaltyBehavior Behavior { get; init; } = AfkPenaltyBehavior.PlayerInput;
    [YamlMember(Description = "Which models are excluded from this penalty. Example: Spectator slots")]
    public List<string> ExcludedModels { get; set; } = [];

    [YamlIgnore] public int DurationMilliseconds => DurationMinutes * 60_000;
}

public enum AfkPenaltyBehavior
{
    PlayerInput,
    MinimumSpeed
}

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class HighPingPenaltyConfiguration
{
    [YamlMember(Description = "Set to true to enable")]
    public bool Enabled { get; set; } = true;
    [YamlMember(Description = "Time after the player gets kicked. A warning will be sent in chat after half this time")]
    public int DurationSeconds { get; set; } = 20;
    [YamlMember(Description = "Players having a lower ping will not be kicked")]
    public int MaximumPingMilliseconds { get; set; } = 500;
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
    public int PitsBeforeKick { get; set; } = 2;

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
    public int PitsBeforeKick { get; set; } = 2;
    
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
    public int PitsBeforeKick { get; set; } = 2;
    
    [YamlIgnore] public float MaximumSpeedMs => MaximumSpeedKph / 3.6f;
}
