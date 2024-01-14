using AssettoServer.Server.Configuration;
using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace WordFilterPlugin;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class WordFilterConfiguration : IValidateConfiguration<WordFilterConfigurationValidator>
{
    [YamlMember(Description = "Username patterns it won't be possible to join with")]
    public List<string> ProhibitedUsernamePatterns { get; init; } = [];
    [YamlMember(Description = "Chat message patterns that will not be broadcasted to other players")]
    public List<string> ProhibitedChatPatterns { get; init; } = [];
    [YamlMember(Description = "Chat message patterns that will automatically ban the player on sending them")]
    public List<string> BannableChatPatterns { get; init; } = [];
}
