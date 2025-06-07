using AssettoServer.Commands;
using AssettoServer.Commands.Attributes;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using Qmmands;

namespace TagModePlugin;

[RequireConnectedPlayer]
public class TagModeCommandModule : ACModuleBase
{
    private readonly TagModePlugin _plugin;

    public TagModeCommandModule(TagModePlugin plugin)
    {
        _plugin = plugin;
    }

    [Command("tagstart"), RequireConnectedPlayer, RequireAdmin]
    public async ValueTask Start([Remainder] ACTcpClient? player = null)
    {
        var starter = player?.EntryCar;
        if (starter == null && !_plugin.TryPickRandomTagger(out starter))
        {
            Reply("Unable to pick a random tagger.");
            return;
        }
        
        if (await _plugin.TryStartSession(starter))
            Reply("Session is started.");
        else
            Reply("Session could not be started.");
    }

    [Command("tagcancel"), RequireConnectedPlayer, RequireAdmin]
    public void Cancel()
    {
        if (_plugin.CurrentSession != null)
            _plugin.CurrentSession.Cancel();
        else
            Reply("No session in progress.");
    }
}
