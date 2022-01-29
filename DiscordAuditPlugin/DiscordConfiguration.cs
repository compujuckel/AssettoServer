using System.Diagnostics.CodeAnalysis;

namespace DiscordAuditPlugin;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class DiscordConfiguration
{
    public string? PictureUrl { get; init; }
    public string? AuditUrl { get; init; }
    public string? ChatUrl { get; init; }
    public bool ChatMessageIncludeServerName { get; init; } = false;
}