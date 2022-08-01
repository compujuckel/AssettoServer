using AssettoServer.Commands;
using AssettoServer.Commands.Modules;
using AssettoServer.Network.Tcp;
using Qmmands;

namespace RaceChallengePlugin;

[RequireConnectedPlayer]
public class RaceCommandModule : ACModuleBase
{
    private readonly RaceChallengePlugin _plugin;

    public RaceCommandModule(RaceChallengePlugin plugin)
    {
        _plugin = plugin;
    }

    [Command("race")]
    public void Race(ACTcpClient player)
        => _plugin.GetRace(Context.Client!.EntryCar).ChallengeCar(player.EntryCar);

    [Command("accept")]
    public async ValueTask AcceptRaceAsync()
    {
        var currentRace = _plugin.GetRace(Context.Client!.EntryCar).CurrentRace;
        if (currentRace == null)
            Reply("You do not have a pending race request.");
        else if (currentRace.HasStarted)
            Reply("This race has already started.");
        else
            await currentRace.StartAsync();
    }
}
