using AssettoServer.Server.Configuration;
using JetBrains.Annotations;
using VotingPresetPlugin.Preset;
using YamlDotNet.Serialization;

namespace VotingPresetPlugin;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class VotingPresetConfiguration : IValidateConfiguration<VotingPresetConfigurationValidator>
{
    // General settings
    [YamlMember(Description = "Reconnect clients instead of kicking when restart is initiated. \nPlease disable reconnect with varying entry lists in the presets")]
    public bool ReconnectEnabled { get; init; } = true;
    
    // Voting settings
    [YamlMember(Description = "Enable Voting")]
    public bool VoteEnabled { get; init; } = true; 
    
    [YamlMember(Description = "Number of choices players can choose from at each voting interval")]
    public int VoteChoices { get; init; } = 3;
    
    [YamlMember(Description = "Will preset/track change randomly with equal odds if no vote has been counted")]
    public bool ChangePresetWithoutVotes { get; init; } = false;
    
    [YamlMember(Description = "Whether the current preset/track should be part of the next vote.")]
    public bool IncludeStayOnTrackVote { get; init; } = true;
    
    [YamlMember(Description = "Whether to skip the comparison of the starting entry list to the presets entry lists.")]
    public bool SkipEntryListCheck { get; init; } = false;
    
    // Cycle numbers :)
    [YamlMember(Description = "How often a cycle/vote takes place. Minimum 5, Default 90")]
    public int VotingIntervalMinutes { get; init; } = 90;
    
    [YamlMember(Description = "How long the vote stays open. Minimum 10, Default 300")]
    public int VotingDurationSeconds { get; init; } = 300;
    
    [YamlMember(Description = "How long it takes before notifying. Minimum 0, Default 10")]
    public int DelayTransitionDurationSeconds { get; init; } = 0;
    
    [YamlMember(Description = "How long it takes to change the preset/track after notifying. Minimum 2, Default 5")]
    public int TransitionDurationSeconds { get; init; } = 5;
    
    // Metadata / former preset_cfg.yml
    [YamlMember(Description = "Preset specific settings \nThe cfg/ directory is always ignored for the presets pool.")]
    public PresetConfiguration Meta { get; init; } = new();
    

    [YamlIgnore] public int VotingIntervalMilliseconds => VotingIntervalMinutes * 60_000;
    [YamlIgnore] public int VotingDurationMilliseconds => VotingDurationSeconds * 1000;
    [YamlIgnore] public int TransitionDurationMilliseconds => TransitionDurationSeconds * 1000;
    [YamlIgnore] public int DelayTransitionDurationMilliseconds => DelayTransitionDurationSeconds * 1000;
    
}
