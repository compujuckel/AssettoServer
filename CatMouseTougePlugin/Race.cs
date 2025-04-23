using System.Numerics;
using AssettoServer.Network.ClientMessages;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using Microsoft.AspNetCore.Identity.UI.Services;
using Serilog;
using CatMouseTougePlugin.Packets;

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
    private TaskCompletionSource<bool> _followerFirst = new();

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
    }

    public async Task<EntryCar?> RaceAsync()
    {
        Log.Debug("Starting race.");
        EntryCar? winner = null;
        try
        {
            SendMessage("Teleporting to starting positions...(with new method)");
            // First teleport players to their starting positions.
            Leader.Client!.SendPacket(new TeleportPacket
            {
                Position = new Vector3(-204.4f, 468.34f, -93.87f),  // Your target position
                Direction = new Vector3(0.0998f, 0.992f, 0.0784f),  // Forward direction (can be approximate)
            });
            Follower.Client!.SendPacket(new TeleportPacket
            {
                Position = new Vector3(-198.89f, 468.01f, -88.14f),
                Direction = new Vector3(0.0919f, 0.992f, 0.0832f), // Forward direction (example)
            });

            // Maybe reset their current lap so they dont unintentionally set a lap when crossing the finish line again.
            HasStarted = true;
            // Make a race countdown.
            SendMessage("Go! Race started!");
            // Start the race.
            // Let the cars do a lap
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

}


