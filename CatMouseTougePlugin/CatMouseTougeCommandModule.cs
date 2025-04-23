using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using AssettoServer.Commands;
using AssettoServer.Commands.Attributes;
using AssettoServer.Server;
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
}
