using AssettoServer.Server.Configuration;
using JetBrains.Annotations;
using VotingPresetPlugin.Preset;
using YamlDotNet.Serialization;

namespace VotingPresetPlugin;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class VotingPresetConfiguration : IValidateConfiguration<VotingPresetConfigurationValidator>
{
    [YamlMember(Description = "Reconnect clients instead of kicking when restart is initiated. \nIt's recommended to set it to false with varying entry lists between the presets")]
    public bool EnableReconnect { get; init; } = true;
    
    [YamlMember(Description = "Enable Preset voting. Set this to false to enable automatic preset cycling")]
    public bool EnableVote { get; init; } = true; 
    
    [YamlMember(Description = "Number of choices available to players in a vote")]
    public int VoteChoices { get; init; } = 3;
    
    [YamlMember(Description = "If set to true, current preset/track will be included in the randomized cycling \nand will always be added to a vote as the first choice")]
    public bool EnableStayOnTrack { get; init; } = true;
    
    [YamlMember(Description = "Will preset/track change randomly with equal odds if no vote has been counted")]
    public bool ChangePresetWithoutVotes { get; init; } = false;
    
    [YamlMember(Description = "Whether to skip the comparison of the starting entry list to the presets entry lists.")]
    public bool SkipEntryListCheck { get; init; } = false;
    
    [YamlMember(Description = "Time between votes or automatic preset cycles. \nMinimum 5, Default 90")]
    public int IntervalMinutes { get; init; } = 90;
    
    [YamlMember(Description = "How long a vote stays open. \nMinimum 10, Default 300")]
    public int VotingDurationSeconds { get; init; } = 300;
    
    [YamlMember(Description = "Time between end of vote and restart notification. \nMinimum 0, Default 10")]
    public int TransitionDelaySeconds { get; init; } = 10;
    
    [YamlMember(Description = "Time between restart notification and restart. \nMinimum 2, Default 5")]
    public int TransitionDurationSeconds { get; init; } = 5;
    
    [YamlMember(Description = "Preset specific settings \nThe cfg/ directory is always ignored for the presets pool.")]
    public PresetConfiguration Meta { get; init; } = new();
    

    [YamlIgnore] public int IntervalMilliseconds => IntervalMinutes * 60_000;
    [YamlIgnore] public int VotingDurationMilliseconds => VotingDurationSeconds * 1000;
    [YamlIgnore] public int TransitionDurationMilliseconds => TransitionDurationSeconds * 1000;
    [YamlIgnore] public int TransitionDelayMilliseconds => TransitionDelaySeconds * 1000;
    
}
