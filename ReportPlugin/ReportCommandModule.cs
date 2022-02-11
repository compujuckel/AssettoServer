using AssettoServer.Commands;
using JetBrains.Annotations;
using Qmmands;

namespace ReportPlugin;

[UsedImplicitly(ImplicitUseKindFlags.Access, ImplicitUseTargetFlags.WithMembers)]
public class ReportCommandModule : ACModuleBase
{
    [Command("report")]
    public async Task Report([Remainder] string reason)
    {
        var report = Context.Client.GetLastReplay();

        if (report == null)
        {
            Reply("No replay submitted! Press Ctrl+Shift+S to send a replay (CSP 0.1.76+ required)");
        }
        else if (report.Submitted)
        {
            Reply("You have already submitted your last replay.");
        }
        else
        {
            Context.Client.Logger.Information("Report received from {ClientName} ({SessionId}), ID: {Id}, Reason: {Reason}",
                Context.Client.Name, Context.Client.SessionId, report.Guid, reason);
            await ReportPluginHolder.Instance.SubmitReport(Context.Client, report, reason);
            Reply("Your report has been submitted.");
            report.Submitted = true;
        }
    }
}
