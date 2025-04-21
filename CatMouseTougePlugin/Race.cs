using AssettoServer.Server;
using Serilog;

namespace CatMouseTougePlugin;

public class Race
{
    public EntryCar Leader { get; }
    public EntryCar Follower { get;  }

    public bool HasStarted { get; private set; }

    private readonly SessionManager _sessionManager;
    private readonly EntryCarManager _entryCarManager;
    private readonly CatMouseTouge _plugin;

    public delegate Race Factory(EntryCar leader, EntryCar follower);

    public Race(EntryCar leader, EntryCar follower, SessionManager sessionManager, EntryCarManager entryCarManager, CatMouseTouge plugin)
    {
        Leader = leader;
        Follower = follower;
        _sessionManager = sessionManager;
        _entryCarManager = entryCarManager;
        _plugin = plugin;
    }

    public Task StartAsync()
    {
        if (!HasStarted)
        {
            HasStarted = true;
            _ = Task.Run(RaceAsync);
        }

        return Task.CompletedTask;
    }

    private async Task RaceAsync()
    {
        Log.Debug("Race started!");
        var leaderClient = Leader.Client;
        var followerClient = Follower.Client;

        leaderClient.LapCompleted += OnClientLapCompleted;
        followerClient.LapCompleted += OnClientLapCompleted;


        // First teleport players to their starting positions.
        // Make a race countdown.
        // Start the race.
        // Let the cars do a lap
        // Get their next laptime.
        // Compare laptimes to find a winner
        // Return the winner.
    }

}


