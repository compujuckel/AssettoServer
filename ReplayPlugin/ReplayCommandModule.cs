using AssettoServer.Commands;
using AssettoServer.Commands.Attributes;
using AssettoServer.Network.Tcp;
using Qmmands;

namespace ReplayPlugin;

[RequireAdmin]
public class ReplayCommandModule : ACModuleBase
{
    private readonly ReplayService _replayService;

    public ReplayCommandModule(ReplayService replayService)
    {
        _replayService = replayService;
    }

    [Command("replay")]
    public async Task SaveReplayAsync(int seconds, [Remainder] ACTcpClient? client = null)
    {
        var sessionId = client?.SessionId ?? Client?.SessionId;
        
        if (sessionId == null)
        {
            Reply("No target player specified");
            return;
        }
        
        await SaveReplayIdAsync(seconds, sessionId.Value);
    }

    [Command("replay_id")]
    public async Task SaveReplayIdAsync(int seconds, byte sessionId)
    {
        var filename = $"replay_{DateTime.Now:yyyyMMdd'T'HHmmss}_{sessionId}.acreplay";
        await _replayService.SaveReplayAsync(seconds, sessionId, filename);
        Reply($"Saved replay {filename}");
    }
}
