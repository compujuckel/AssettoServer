using AssettoServer.Commands;
using AssettoServer.Network.Packets.Outgoing;
using AssettoServer.Network.Tcp;
using Qmmands;

namespace RaceChallengePlugin;

public class RaceCommandModule : ACModuleBase
{
    [Command("race")]
    public void Race(ACTcpClient player)
        => Context.Client.EntryCar.GetRace().ChallengeCar(player.EntryCar);

    [Command("accept")]
    public async ValueTask AcceptRaceAsync()
    {
        Race currentRace = Context.Client.EntryCar.GetRace().CurrentRace;
        if (currentRace == null)
            Reply("You do not have a pending race request.");
        else if (currentRace.HasStarted)
            Reply("This race has already started.");
        else
            await currentRace.StartAsync();
    }

    [Command("healthupdate")]
    public void HealthUpdate(byte rivalId, float ownHealth, float rivalHealth)
    {
        var packet = new RaceHealthUpdate
        {
            OwnHealth = ownHealth,
            RivalHealth = rivalHealth,
            RivalId = rivalId
        };

        Context.Client.SendPacket(packet);
    }
}