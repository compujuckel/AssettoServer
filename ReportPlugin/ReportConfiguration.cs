using JetBrains.Annotations;

namespace ReportPlugin;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class ReportConfiguration
{
    public int ClipDurationSeconds { get; set; } = 60;
    public string? WebhookUrl { get; set; }
}
