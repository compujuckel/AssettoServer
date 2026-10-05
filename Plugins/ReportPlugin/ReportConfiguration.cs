using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace ReportPlugin;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class ReportConfiguration
{
    [YamlMember(Description = "Length of replay clips")]
    public int ClipDurationSeconds { get; set; } = 60;
    [YamlMember(Description = "Discord webhook URL to send reports to. Optional, reports will be logged to the server log if you leave this empty")]
    public string? WebhookUrl { get; set; }
}
