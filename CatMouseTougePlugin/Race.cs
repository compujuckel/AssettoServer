using System.Numerics;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using Serilog;
using CatMouseTougePlugin.Packets;

namespace CatMouseTougePlugin;

public class Race
{
    public EntryCar Leader { get; }
    public EntryCar Follower { get;  }

    public bool HasStarted { get; private set; }

    private readonly Dictionary<string, Vector3> _leaderStartPos = [];
    private readonly Dictionary<string, Vector3> _followerStartPos = [];

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

    public Race(EntryCar leader, EntryCar follower)
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

        // Setting up starting positions.
        // In the future should be loaded from the config file.
        _leaderStartPos.Add("Position", new Vector3(-204.4f, 468.34f, -93.87f));
        _leaderStartPos.Add("Direction", new Vector3(0.0998f, 0.992f, 0.0784f));

        _followerStartPos.Add("Position", new Vector3(-198.89f, 468.01f, -88.14f));
        _followerStartPos.Add("Direction", new Vector3(0.0919f, 0.992f, 0.0832f));
    }

    public async Task<EntryCar?> RaceAsync()
    {
        Log.Debug("Starting race.");
        EntryCar? winner = null;
        bool isGo = false;
        try
        {
            // First teleport players to their starting positions.
            await TeleportToStartAsync(Leader, Follower);

            SendMessage("Race starting soon...");
            await Task.Delay(3000);

            HasStarted = true; // I don't know if this is used anywhere tbh.

            // Race countdown.
            while (!isGo)
            {
                byte signalStage = 0;
                while (signalStage < 3)
                {
                    JumpstartResult jumpstart = AreInStartingPos();
                    if (jumpstart != JumpstartResult.None)
                    {
                        if (jumpstart == JumpstartResult.Both)
                        {
                            SendMessage("Both players made a jumpstart.");
                            SendMessage("Returning both players to starting position.");
                            await TeleportToStartAsync(Leader, Follower);
                            SendMessage("Race restarting soon...");
                            await Task.Delay(3000);
                            break;
                        }
                        else if (jumpstart == JumpstartResult.Follower)
                        {
                            SendMessage($"{FollowerName} made a jumpstart. {LeaderName} wins this race.");
                            return Leader;
                        }
                        else
                        {
                            SendMessage($"{LeaderName} made a jumpstart. {FollowerName} wins this race.");
                            return Follower;
                        }
                    }

                    if (signalStage == 0)
                        _ = SendTimedMessageAsync("Ready...");
                    else if (signalStage == 1)
                        _ = SendTimedMessageAsync("Set...");
                    else if (signalStage == 2)
                    {
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
                SendMessage("Race cancelled due to disconnection.");

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
                    SendMessage($"{LeaderName} did not pull away. {FollowerName} wins!");
                    winner = Follower;
                }
                // Small cooldown time after the race finished.
                await Task.Delay(10000);
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

        return winner;
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
        if (LeaderSetLap && FollowerSetLap)
            secondLapCompleted.TrySetResult(true);

        // If only the leader has set a lap
        else if (LeaderSetLap && !FollowerSetLap)
            _ = Task.Delay(3000).ContinueWith(_ => secondLapCompleted.TrySetResult(false));

        // Overtake, the follower finished earlier than leader.
        else if (FollowerSetLap && !LeaderSetLap)
            _followerFirst.TrySetResult(true);
            
    }

    private void OnClientDisconnected(ACTcpClient sender, EventArgs args)
    {
        _disconnected.TrySetResult(true);
    }

    private async Task TeleportToStartAsync(EntryCar Leader, EntryCar Follower)
    {
        Leader.Client!.SendPacket(new TeleportPacket
        {
            Position = _leaderStartPos["Position"],  
            Direction = _leaderStartPos["Direction"],  
        });
        Follower.Client!.SendPacket(new TeleportPacket
        {
            Position = _followerStartPos["Position"],
            Direction = _followerStartPos["Direction"],
        });

        // Check if both cars have been teleported to their starting locations.
        bool isLeaderTeleported = false;
        bool isFollowerTeleported = false;

        while (!isLeaderTeleported || !isFollowerTeleported)  
        {
            Vector3 currentLeaderPos = Leader.Status.Position;
            Vector3 currentFollowerPos = Follower.Status.Position;
            
            float leaderDistanceSquared = Vector3.DistanceSquared(currentLeaderPos, _leaderStartPos["Position"]);
            float followerDistanceSquared = Vector3.DistanceSquared(currentFollowerPos, _followerStartPos["Position"]);

            const float thresholdSquared = 50f;

            if (leaderDistanceSquared < thresholdSquared) {
                isLeaderTeleported = true;
            }
            if (followerDistanceSquared < thresholdSquared) {
                isFollowerTeleported = true;
            }

            // Wait for a short time before checking again to avoid blocking the thread
            await Task.Delay(100);  // Delay for 100 ms (adjust as necessary)
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
        await Task.Delay(highPingCar.Ping - lowPingCar.Ping);
        lowPingCar.Client?.SendChatMessage(message);
    }

    // Check if the cars are still in their starting positions.
    private JumpstartResult AreInStartingPos()
    {
        // Get the current position of each car.
        Vector3 currentLeaderPos = Leader.Status.Position;
        Vector3 currentFollowerPos = Follower.Status.Position;

        // Check if they are the same as the original starting postion.
        // Or at least check if the difference is not larger than a threshold.
        float leaderDistanceSquared = Vector3.DistanceSquared(currentLeaderPos, _leaderStartPos["Position"]);
        float followerDistanceSquared = Vector3.DistanceSquared(currentFollowerPos, _followerStartPos["Position"]);

        const float thresholdSquared = 100f;

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



}


