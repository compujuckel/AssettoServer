using AssettoServer.Network.Tcp;

namespace ReportPlugin;

public static class ACTcpClientExtensions
{
    public static Replay? GetLastReplay(this ACTcpClient client)
    {
        ReportPluginHolder.Instance.Reports.TryGetValue(client, out var report);
        return report;
    }

    public static void SetLastReplay(this ACTcpClient client, Replay replay) => ReportPluginHolder.Instance.Reports[client] = replay;
}
