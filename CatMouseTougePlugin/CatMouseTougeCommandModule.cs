using System.Numerics;
using AssettoServer.Commands;
using AssettoServer.Commands.Attributes;
using AssettoServer.Server;
using AssettoServer.Shared.Model;
using CatMouseTougePlugin.Packets;
using Qmmands;

namespace CatMouseTougePlugin;

public class CatMouseTougeCommandModule : ACModuleBase
{
    private readonly CatMouseTouge _plugin;

    public CatMouseTougeCommandModule(CatMouseTouge plugin)
    {
        _plugin = plugin;
    }

    [Command("invite"), RequireConnectedPlayer]
    public void Invite()
    {
        // Find the most nearby player if there is any and send them an session invite.
        // In the future along with the chat command it would be nice to have a UI element to invite people.
        
        // Get the closest player
        EntryCar? nearestCar = _plugin.GetSession(Client!.EntryCar).FindNearbyCar();
        if (nearestCar != null)
        {
            _plugin.GetSession(Client!.EntryCar).ChallengeCar(nearestCar);
        }
        else
        {
            Reply("No car nearby!");
        }
        
    }

    [Command("accepttouge"), RequireConnectedPlayer]
    public async ValueTask AcceptInvite()
    {
        var currentSession = _plugin.GetSession(Client!.EntryCar).CurrentSession;
        if (currentSession == null)
            Reply("You do not have a pending touge session invite.");
        else if (currentSession.Challenger == Client!.EntryCar)
            Reply("You cannot accept an invite you sent.");
        else if (currentSession.IsActive)
            Reply("You are already in an active touge session.");
        else
        {
            Reply("Invite succesfully accepted!");
            // This currentSession object is shared among the two players.
            // They both hold a reference to it.
            await currentSession.StartAsync();
        }
    }

    [Command("teleport"), RequireConnectedPlayer]
    public void Teleport()
    {
        // For testing the teleport
        Reply("Teleporting...");

        Client!.SendPacket(new TeleportPacket
        {
            Position = new Vector3(-204.4f, 468.34f, -93.87f),  // Your target position
            Direction = new Vector3(0.0998f, 0.992f, 0.0784f),  // Forward direction (can be approximate)
        });
    }

    [Command("elo"), RequireConnectedPlayer]
    public void Elo()
    {
        string playerId = Client!.Guid.ToString();
        int elo = _plugin.GetPlayerElo(playerId);
        Reply($"You elo is {elo}.");
        Client!.SendPacket(new EloPacket { Elo = elo });
    }
}
