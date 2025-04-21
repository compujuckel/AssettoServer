
using AssettoServer.Server;
using AssettoServer.Shared.Network.Packets.Outgoing;

namespace CatMouseTougePlugin;

public class TougeSession
{
    public EntryCar Challenger { get; }
    public EntryCar Challenged { get; }

    public bool IsActive { get; private set; }
    public bool IsChallenger { get; }

    private string ChallengerName { get; }
    private string ChallengedName { get; }

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

        ChallengerName = Challenger.Client?.Name!;
        ChallengedName = Challenged.Client?.Name!;
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
        // Implement touge session logic here
        // Run 2 races.
        // A Race is an object. Similar to the Race object in RaceChallengePlugin
        Race race1 = _raceFactory(Challenger, Challenged);
        Race race2 = _raceFactory(Challenged, Challenger);
        // After the first two rounds:
        //  Check if there is the need for a third round
        //  If not, there is a winner. Do elo calcs.
        //  If yes, start more races until there is a winner.
        //      Elo calcs
    }
}
