using System.Numerics;
using AssettoServer.Commands;
using AssettoServer.Commands.Attributes;
using TougePlugin.Packets;
using Qmmands;

namespace TougePlugin;

public class TougeCommandModule : ACModuleBase
{
    private readonly Touge _plugin;

    public TougeCommandModule(Touge plugin)
    {
        _plugin = plugin;
    }

    [Command("invite"), RequireConnectedPlayer]
    public void Invite()
    {
        _plugin.InviteNearbyCar(Client!);  
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
}
