using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using Microsoft.AspNetCore.Identity.UI.Services;
using Serilog;

namespace CatMouseTougePlugin;

public class Race
{
    public EntryCar Leader { get; }
    public EntryCar Follower { get;  }

    public bool HasStarted { get; private set; }

    private bool LeaderSetLap = false;
    private bool FollowerSetLap = false;
    private TaskCompletionSource<bool> secondLapCompleted = new();
    private TaskCompletionSource<bool> _disconnected = new();

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

        // Event handling
        Leader.Client!.LapCompleted += OnClientLapCompleted;
        Follower.Client!.LapCompleted += OnClientLapCompleted;
        Leader.Client.Disconnecting += OnClientDisconnected;
        Follower.Client.Disconnecting += OnClientDisconnected;
    }

    public async Task RaceAsync()
    {
        Log.Debug("Starting race.");
        try
        {
            // First teleport players to their starting positions.
            // Maybe reset their current lap so they dont unintentionally set a lap when crossing the finish line again.
            HasStarted = true;
            // Make a race countdown.
            SendMessage("Go! Race started!");
            // Start the race.
            // Let the cars do a lap
            Task completed = await Task.WhenAny(secondLapCompleted.Task, _disconnected.Task);

            if (completed == _disconnected.Task)
                SendMessage("Race cancelled due to disconnection.");

            else
            {
                // Who wins logic
                if (!FollowerSetLap)
                    SendMessage("Follower did not finish in time. Leader wins!");
                else if (!LeaderSetLap)
                    SendMessage("Leader did not finish in time. Follower wins!");
                else
                    SendMessage("Both players set a lap time!");
                // Get their last laptime.
                // Compare laptimes to find a winner
                // Return the winner.
            }
        }

        catch (Exception e)
        {
            Log.Error(e, "Error while running race.");
            SendMessage("There was an error while runnning the race.");
        }
        finally
        {
            FinishRace();
        }
        
    }

    private void FinishRace()
    {
        // Clean up
        Leader.Client!.LapCompleted -= OnClientLapCompleted;
        Follower.Client!.LapCompleted -= OnClientLapCompleted;
        Leader.Client.Disconnecting -= OnClientDisconnected;
        Follower.Client.Disconnecting -= OnClientDisconnected;
    }

    private void SendMessage(string message)
    {
        Follower.Client?.SendChatMessage(message);
        Leader.Client?.SendChatMessage(message);
    }

    private void OnClientLapCompleted(ACTcpClient sender, LapCompletedEventArgs args)
    {
        var car = sender.EntryCar;
        if (car == Leader)
            LeaderSetLap = true;
        else if (car == Follower)
            FollowerSetLap = true;

        // If someone already set a lap, and this is the seconds person to set a lap
        if(LeaderSetLap && FollowerSetLap)
            secondLapCompleted.TrySetResult(true);
        // If only one person has completed a lap.
        else if (FollowerSetLap || LeaderSetLap)
            _ = Task.Delay(30000).ContinueWith(_ => secondLapCompleted.TrySetResult(false));
    }

    private void OnClientDisconnected(ACTcpClient sender, EventArgs args)
    {
        SendMessage("A player disconnected. Ending the race.");
        _disconnected.TrySetResult(true);
    }

}


