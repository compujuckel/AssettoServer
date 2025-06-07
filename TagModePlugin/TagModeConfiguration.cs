using AssettoServer.Server.Configuration;
using AssettoServer.Shared.Weather;
using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace TagModePlugin;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class TagModeConfiguration : IValidateConfiguration<TagModeConfigurationValidator>
{
    [YamlMember(Description = "Should a session only end when all runners have been caught.\nIf set to true, the winner will be the last runner that was caught.\nIf set to false, if at least one runner was not caught when the time runs out, the runners win")]
    public bool EnableEndlessMode { get; init; } = false;
    [YamlMember(Description = "Should tag sessions be started automatically")]
    public bool EnableLoop { get; init; } = false;
    [YamlMember(Description = "If this is set to 'true' late joiners will join an active game as a runner. \nIf this is set to 'false' late joiners will join an active game as a tagger")]
    public bool EnableLateJoinRunner { get; init; } = true;
    [YamlMember(Description = "How long a session last, if all players are tagged, the session also ends")]
    public int SessionDurationMinutes { get; init; } = 5;
    [YamlMember(Description = "How long the pause between sessions is")]
    public int SessionPauseIntervalMinutes { get; init; } = 2;
    
    [YamlMember(Description = "Color used when no session is active")]
    public string NeutralColor { get; init; } = "#348feb";
    [YamlMember(Description = "Color used when running from the taggers")]
    public string RunnerColor { get; init; } = "#dbdbdb";
    [YamlMember(Description = "Color when hit by the tagger/joining the taggers")]
    public string TaggedColor { get; init; } = "#fa0000";

    [YamlIgnore] public int SessionPauseIntervalMilliseconds => SessionPauseIntervalMinutes * 60_000;
    [YamlIgnore] public long SessionDurationMilliseconds => SessionDurationMinutes * 60_000;
}
