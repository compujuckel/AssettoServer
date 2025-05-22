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
    public async ValueTask Start([Remainder] ACTcpClient player)
    {
        if (await _plugin.TryStartSession(player.EntryCar))
            Reply("Session is started.");
        else
            Reply("Session could not be started.");
    }

    [Command("tagstartrandom"), RequireConnectedPlayer, RequireAdmin]
    public async ValueTask StartRandom()
    {
        if (!_plugin.TryPickRandomTagger(out var randomTagger))
        {
            Reply("Unable to pick a random tagger.");
            return;
        }
        
        if (await _plugin.TryStartSession(randomTagger))
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
