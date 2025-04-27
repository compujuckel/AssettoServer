using AssettoServer.Commands;
using AssettoServer.Commands.Attributes;
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

    [Command("race"), RequireConnectedPlayer]
    public void Race(ACTcpClient player)
        => _plugin.GetRace(Client!.EntryCar).ChallengeCar(player.EntryCar);

    [Command("accept"), RequireConnectedPlayer]
    public async ValueTask AcceptRaceAsync()
    {
        var currentRace = _plugin.GetRace(Client!.EntryCar).CurrentRace;
        if (currentRace == null)
            Reply("You do not have a pending race request.");
        else if (currentRace.HasStarted)
            Reply("This race has already started.");
        else if (currentRace.Challenger == Client!.EntryCar)
            Reply("You cannot accept a race you initiated.");
        else
            await currentRace.StartAsync();
    }
}
