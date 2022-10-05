using AssettoServer.Server.Configuration;
using JetBrains.Annotations;

namespace WordFilterPlugin;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class WordFilterConfiguration : IValidateConfiguration<WordFilterConfigurationValidator>
{
    public List<string> ProhibitedUsernamePatterns { get; init; } = new();

    public List<string> ProhibitedChatPatterns { get; init; } = new();
    public List<string> BannableChatPatterns { get; init; } = new();
}
