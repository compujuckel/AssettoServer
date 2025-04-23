
using AssettoServer.Server;
using AssettoServer.Shared.Network.Packets.Outgoing;
using Serilog;

namespace CatMouseTougePlugin;

public class TougeSession
{
    public EntryCar Challenger { get; }
    public EntryCar Challenged { get; }

    public bool IsActive { get; private set; }

    private readonly SessionManager _sessionManager;
    private readonly EntryCarManager _entryCarManager;
    private readonly CatMouseTouge _plugin;
    private readonly Race.Factory _raceFactory;

    public delegate TougeSession Factory(EntryCar challenger, EntryCar challenged);

    public TougeSession(EntryCar challenger, EntryCar challenged, SessionManager sessionManager, EntryCarManager entryCarManager, CatMouseTouge plugin, Race.Factory raceFactory)
    {
        Challenger = challenger;
        Challenged = challenged;
        _sessionManager = sessionManager;
        _entryCarManager = entryCarManager;
        _plugin = plugin;
        _raceFactory = raceFactory;
    }

    public Task StartAsync()
    {
        if(!IsActive)
        {
            IsActive = true;
            _ = Task.Run(TougeSessionAsync);
        }

        return Task.CompletedTask;
    }

    private async Task TougeSessionAsync()
    {
        try
        {
            // Currently there is no time between races to line up or get teleported.
            // Implement touge session logic here
            // Run 2 races.
            // A Race is an object. Similar to the Race object in RaceChallengePlugin
            // Have to check as well if the players are still connected.
            Race race1 = _raceFactory(Challenger, Challenged);
            EntryCar? winner1 = await race1.RaceAsync();
            // Because its important the race is finished before starting the next one.
            if (winner1 != null)
            {
                Race race2 = _raceFactory(Challenged, Challenger);
                EntryCar? winner2 = await race2.RaceAsync();

                if (winner2 != null)
                {
                    if (winner1 == winner2)
                    {
                        // Someone won both rounds so that is the winner.
                    }
                    else
                    {
                        Race race3 = _raceFactory(Challenger, Challenged);
                        EntryCar? winner3 = await race3.RaceAsync();
                        // Winner 3 is the overall winner.
                    }
                }
            }

            // After the first two rounds:
            //  Check if there is the need for a third round
            //  If not, there is a winner. Do elo calcs.
            //  If yes, start more races until there is a winner.
            //      Elo calcs
        }
        catch (Exception ex) 
        {
            Log.Error(ex, "Error while running touge session.");
        }
        finally
        {
            FinishTougeSession();
        }
    }

    private void FinishTougeSession()
    {
        _plugin.GetSession(Challenger).CurrentSession = null;
        _plugin.GetSession(Challenged).CurrentSession = null;

        string ChallengedName = Challenged.Client?.Name!;
        string ChallengerName = Challenger.Client?.Name!;

        _entryCarManager.BroadcastChat($"Race between {ChallengerName} and {ChallengedName} has concluded!");
    }
}
