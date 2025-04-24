
using AssettoServer.Server;
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
            EntryCar? overallWinner = null;
            // Run race 1.
            Race race1 = _raceFactory(Challenger, Challenged);
            EntryCar? winner1 = await race1.RaceAsync();
            // Because its important the race is finished before starting the next one.
            if (winner1 != null)
            {
                Race race2 = _raceFactory(Challenged, Challenger);
                EntryCar? winner2 = await race2.RaceAsync();

                if (winner2 != null)
                {
                    if (winner1 != winner2)
                    {
                        Race race3 = _raceFactory(Challenger, Challenged);
                        overallWinner = await race3.RaceAsync(); // The overall winner.
                    }
                    else
                    {
                        overallWinner = winner1;
                    }
                }
            }

            // Calculate ELO.
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
