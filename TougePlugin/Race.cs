using System.Numerics;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using Serilog;
using TougePlugin.Packets;

namespace TougePlugin;

public class Race
{
    public EntryCar Leader { get; }
    public EntryCar Follower { get;  }

    private readonly EntryCarManager _entryCarManager;
    private readonly TougeConfiguration _configuration;

    public bool HasStarted { get; private set; }

    public enum JumpstartResult
    {
        None,            // No jumpstart
        Leader,          // Leader performed a jumpstart
        Follower,        // Follower performed a jumpstart
        Both             // Both performed a jumpstart (if both are outside the threshold)
    }

    private bool LeaderSetLap = false;
    private bool FollowerSetLap = false;
    private readonly TaskCompletionSource<bool> secondLapCompleted = new();
    private readonly TaskCompletionSource<bool> _disconnected = new();
    private readonly TaskCompletionSource<bool> _followerFirst = new();

    private string LeaderName { get; }
    private string FollowerName { get; }

    public delegate Race Factory(EntryCar leader, EntryCar follower);

    public Race(EntryCar leader, EntryCar follower, EntryCarManager entryCarManager, TougeConfiguration configuration)
    {
        Leader = leader;
        Follower = follower;

        LeaderName = Leader.Client?.Name!;
        FollowerName = Follower.Client?.Name!;

        // Event handling
        Leader.Client!.LapCompleted += OnClientLapCompleted;
        Follower.Client!.LapCompleted += OnClientLapCompleted;
        Leader.Client.Disconnecting += OnClientDisconnected;
        Follower.Client.Disconnecting += OnClientDisconnected;

        _entryCarManager = entryCarManager;
        _configuration = configuration;
    }

    public async Task<RaceResult> RaceAsync()
    {
        Log.Debug("Starting race.");
        EntryCar? winner = null;
        bool isGo = false;
        try
        {
            Dictionary<string, Vector3>[] startingArea = await GetStartingAreaAsync();

            // First teleport players to their starting positions.
            await TeleportToStartAsync(Leader, Follower, startingArea);

            SendMessage("Race starting soon...");
            await Task.Delay(3000);

            HasStarted = true; // I don't know if this is used anywhere tbh.

            // Race countdown.
            while (!isGo)
            {
                byte signalStage = 0;
                while (signalStage < 3)
                {
                    if (!_configuration.isRollingStart)
                    {
                        JumpstartResult jumpstart = AreInStartingPos(startingArea);
                        if (jumpstart != JumpstartResult.None)
                        {
                            if (jumpstart == JumpstartResult.Both)
                            {
                                SendMessage("Both players made a jumpstart.");
                                await RestartRaceAsync();
                                break;
                            }
                            else if (jumpstart == JumpstartResult.Follower)
                            {
                                SendMessage($"{FollowerName} made a jumpstart. {LeaderName} wins this race.");
                                return RaceResult.Win(Leader);
                            }
                            else
                            {
                                SendMessage($"{LeaderName} made a jumpstart. {FollowerName} wins this race.");
                                return RaceResult.Win(Follower);
                            }
                        }
                    }

                    if (signalStage == 0)
                        _ = SendTimedMessageAsync("Ready...");
                    else if (signalStage == 1)
                        _ = SendTimedMessageAsync("Set...");
                    else if (signalStage == 2)
                    {
                        if (_configuration.isRollingStart)
                        {
                            // Check if cars are close enough to each other to give a valid "Go!".
                            if (!IsValidRollingStartPos())
                            {
                                SendMessage("Players are not close enough for a fair rolling start.");
                                await RestartRaceAsync();
                                break;
                            }
                        }
                        _ = SendTimedMessageAsync("Go!");
                        isGo = true;
                        break;
                    }

                    await Task.Delay(1000);
                    signalStage++;
                }
            }

            // Start the race.
            // Let the cars do a lap/complete the course.
            Task completed = await Task.WhenAny(secondLapCompleted.Task, _disconnected.Task, _followerFirst.Task);

            if (completed == _disconnected.Task)
            {
                SendMessage("Race cancelled due to disconnection.");
                return RaceResult.Disconnected();
            }

            else
            {
                // Who wins logic
                if (!FollowerSetLap)
                {
                    SendMessage($"{FollowerName} did not finish in time. {LeaderName} wins!");
                    winner = Leader;
                }
                else if (completed == _followerFirst.Task)
                {
                    SendMessage($"{FollowerName} overtook {LeaderName}. {FollowerName} wins!");
                    winner = Follower;
                }
                else
                {
                    SendMessage($"{LeaderName} did not pull away. It's a tie!");
                    return RaceResult.Tie();
                }
            }
        }

        catch (Exception e)
        {
            Log.Error(e, "Error while running race.");
            SendMessage("There was an error while runnning the race.");
            return RaceResult.Tie();
        }
        finally
        {
            FinishRace();
        }

        return RaceResult.Win(winner);
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
        Touge.SendNotification(Follower.Client, message);
        Touge.SendNotification(Leader.Client, message);
    }

    private async Task RestartRaceAsync()
    {
        SendMessage("Returning both players to their starting positions.");
        SendMessage("Race restarting soon...");
        await Task.Delay(3000);
        Dictionary<string, Vector3>[] startingArea = await GetStartingAreaAsync();
        await TeleportToStartAsync(Leader, Follower, startingArea);
    }

    private void OnClientLapCompleted(ACTcpClient sender, LapCompletedEventArgs args)
    {
        var car = sender.EntryCar;
        if (car == Leader)
            LeaderSetLap = true;
        else if (car == Follower)
            FollowerSetLap = true;

        // If someone already set a lap, and this is the seconds person to set a lap
        if (LeaderSetLap && FollowerSetLap)
            secondLapCompleted.TrySetResult(true);

        // If only the leader has set a lap
        else if (LeaderSetLap && !FollowerSetLap)
        {
            // Make this time also configurable as outrun time.
            int outrunTimer = _configuration.outrunTime * 1000;
            _ = Task.Delay(outrunTimer).ContinueWith(_ => secondLapCompleted.TrySetResult(false));
        }

        // Overtake, the follower finished earlier than leader.
        else if (FollowerSetLap && !LeaderSetLap)
            _followerFirst.TrySetResult(true);    
    }

    private void OnClientDisconnected(ACTcpClient sender, EventArgs args)
    {
        _disconnected.TrySetResult(true);
    }

    private async Task TeleportToStartAsync(EntryCar Leader, EntryCar Follower, Dictionary<string, Vector3>[] startingArea)
    {
        Leader.Client!.SendPacket(new TeleportPacket
        {
            Position = startingArea[0]["Position"],  
            Direction = startingArea[0]["Direction"],  
        });
        Follower.Client!.SendPacket(new TeleportPacket
        {
            Position = startingArea[1]["Position"],
            Direction = startingArea[1]["Direction"],
        });

        // Check if both cars have been teleported to their starting locations.
        bool isLeaderTeleported = false;
        bool isFollowerTeleported = false;

        while (!isLeaderTeleported || !isFollowerTeleported)  
        {
            Vector3 currentLeaderPos = Leader.Status.Position;
            Vector3 currentFollowerPos = Follower.Status.Position;
            
            float leaderDistanceSquared = Vector3.DistanceSquared(currentLeaderPos, startingArea[0]["Position"]);
            float followerDistanceSquared = Vector3.DistanceSquared(currentFollowerPos, startingArea[1]["Position"]);

            const float thresholdSquared = 50f;

            if (leaderDistanceSquared < thresholdSquared) {
                isLeaderTeleported = true;
            }
            if (followerDistanceSquared < thresholdSquared) {
                isFollowerTeleported = true;
            }

            await Task.Delay(250); 
        }
    }

    private async Task SendTimedMessageAsync(string message)
    {
        bool isChallengerHighPing = Leader.Ping > Follower.Ping;
        EntryCar highPingCar, lowPingCar;

        if (isChallengerHighPing)
        {
            highPingCar = Leader;
            lowPingCar = Follower;
        }
        else
        {
            highPingCar = Follower;
            lowPingCar = Leader;
        }

        highPingCar.Client?.SendChatMessage(message);
        Touge.SendNotification(highPingCar.Client, message);
        await Task.Delay(highPingCar.Ping - lowPingCar.Ping);
        lowPingCar.Client?.SendChatMessage(message);
        Touge.SendNotification(lowPingCar.Client, message);
    }

    // Check if the cars are still in their starting positions.
    private JumpstartResult AreInStartingPos(Dictionary<string, Vector3>[] startingArea)
    {
        // Get the current position of each car.
        Vector3 currentLeaderPos = Leader.Status.Position;
        Vector3 currentFollowerPos = Follower.Status.Position;

        // Check if they are the same as the original starting postion.
        // Or at least check if the difference is not larger than a threshold.
        float leaderDistanceSquared = Vector3.DistanceSquared(currentLeaderPos, startingArea[0]["Position"]);
        float followerDistanceSquared = Vector3.DistanceSquared(currentFollowerPos, startingArea[1]["Position"]);

        const float thresholdSquared = 40f;

        // Check if either car has moved too far (jumpstart detection)
        if (leaderDistanceSquared > thresholdSquared && followerDistanceSquared > thresholdSquared)
        {
            // Both cars moved too far
            return JumpstartResult.Both;
        }
        else if (leaderDistanceSquared > thresholdSquared)
        {
            return JumpstartResult.Leader; // Leader caused the jumpstart
        }
        else if (followerDistanceSquared > thresholdSquared)
        {
            return JumpstartResult.Follower; // Follower caused the jumpstart
        }

        return JumpstartResult.None; // No jumpstart
    }

    private bool IsValidRollingStartPos()
    {
        // Check if players are within a certain distance of each other.
        float distanceBetweenCars = Vector3.DistanceSquared(Follower.Status.Position, Leader.Status.Position);
        if (distanceBetweenCars > 30)
            return false;
        return true;
    }

    private Dictionary<string, Vector3>[]? FindClearStartArea()
    {
        // Loop over the list of starting positions in the cfg file
        // If you find a valid/clear starting pos, return that.
        foreach (var startingArea in _configuration.StartingPositions)
        {
            if (IsStartPosClear(startingArea[0]["Position"]) && IsStartPosClear(startingArea[1]["Position"]))
            {
                return startingArea;
            }
        }
        return null;
    }

    private bool IsStartPosClear(Vector3 startPos)
    {
        // Checks if startPos is clear.
        const float startArea = 50f; // Area around the startpoint that should be cleared.
        foreach (var car in _entryCarManager.EntryCars)
        {
            // Dont look at the players in the race or empty cars
            ACTcpClient? carClient = car.Client;
            if (carClient != null && car != Leader && car != Follower)
            {
                // Check if they are not in the starting area.
                float distanceToStartPosSquared = Vector3.DistanceSquared(car.Status.Position, startPos);
                if (distanceToStartPosSquared < startArea)
                {
                    // The car is in the start area.
                    return false;
                }
            }
        }
        return true;
    }

    private async Task<Dictionary<string, Vector3>[]> GetStartingAreaAsync()
    {
        // Get the startingArea here.
        int waitTime = 0;
        Dictionary<string, Vector3>[]? startingArea = FindClearStartArea();
        while (startingArea == null)
        {
            // Wait for a short time before checking again to avoid blocking the thread
            await Task.Delay(250);

            // Try to find a starting area again
            startingArea = FindClearStartArea();

            waitTime += 1;
            if (waitTime > 40)
            {
                // Fallback after 10 seconds.
                startingArea = _configuration.StartingPositions[0];
            }
        }
        return startingArea;
    }
}
