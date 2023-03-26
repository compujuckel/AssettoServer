using JetBrains.Annotations;

namespace DiscordAuditPlugin;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class DiscordConfiguration
{
    public string? PictureUrl { get; init; }
    public string? AuditUrl { get; init; }
    public string? ChatUrl { get; init; }
    public bool ChatMessageIncludeServerName { get; init; } = false;
    public List<ulong> ChatIgnoreGuids { get; init; } = new();
}
