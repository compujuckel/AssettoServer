using AssettoServer.Commands;
using AssettoServer.Commands.Attributes;
using AssettoServer.Network.Tcp;
using Qmmands;
using SerilogTimings;

namespace ReplayPlugin;

[RequireAdmin]
public class ReplayCommandModule : ACModuleBase
{
    private readonly ReplayManager _replayManager;

    public ReplayCommandModule(ReplayManager replayManager)
    {
        _replayManager = replayManager;
    }

    [Command("replay")]
    public void SaveReplay(int seconds, ACTcpClient? client = null)
    {
        var sessionId = client?.SessionId ?? Client?.SessionId;
        
        if (sessionId == null)
        {
            Reply("No target player specified");
            return;
        }
        
        SaveReplayId(seconds, sessionId.Value);
    }

    [Command("replay_id")]
    public void SaveReplayId(int seconds, byte sessionId)
    {
        var filename = $"replay_{DateTime.Now:yyyyMMdd'T'HHmmss}_{sessionId}.acreplay";

        using (var t = Operation.Time("Writing replay {0}", filename))
        {
            _replayManager.WriteReplay(seconds, sessionId, filename);
        }
        Reply($"Saved replay {filename}");
    }
}
