using AssettoServer.Commands;
using AssettoServer.Commands.Attributes;
using Qmmands;

namespace ReportPlugin;

public class ReportCommandModule : ACModuleBase
{
    private readonly ReportPlugin _plugin;

    public ReportCommandModule(ReportPlugin plugin)
    {
        _plugin = plugin;
    }

    [Command("report"), RequireConnectedPlayer]
    public async Task Report([Remainder] string reason)
    {
        var report = _plugin.GetLastReplay(Client!);

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
            Client!.Logger.Information("Report received from {ClientName} ({SessionId}), ID: {Id}, Reason: {Reason}",
                Client.Name, Client.SessionId, report.Guid, reason);
            await _plugin.SubmitReport(Client, report, reason);
            Reply("Your report has been submitted.");
            report.Submitted = true;
        }
    }
}
