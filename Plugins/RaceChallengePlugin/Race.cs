using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using AssettoServer.Server;
using Serilog;

namespace RaceChallengePlugin;

public class Race
{
    public EntryCar Challenger { get; }
    public EntryCar Challenged { get; }
    public EntryCar? Leader { get; private set; }
    public EntryCar? Follower { get; private set; }

    public bool HasStarted { get; private set; }
    public bool LineUpRequired { get; }

    private long LastOvertakeTime { get; set; }
    private Vector3 LastLeaderPosition { get; set; }
    private string ChallengerName { get; }
    private string ChallengedName { get; }

    private readonly SessionManager _sessionManager;
    private readonly EntryCarManager _entryCarManager;
    private readonly RaceChallengePlugin _plugin;

    public delegate Race Factory(EntryCar challenger, EntryCar challenged, bool lineUpRequired = true);
    
    public Race(EntryCar challenger, EntryCar challenged, SessionManager sessionManager, EntryCarManager entryCarManager, RaceChallengePlugin plugin, bool lineUpRequired = true)
    {
        Challenger = challenger;
        Challenged = challenged;
        _sessionManager = sessionManager;
        _entryCarManager = entryCarManager;
        _plugin = plugin;
        LineUpRequired = lineUpRequired;

        ChallengerName = Challenger.Client?.Name!;
        ChallengedName = Challenged.Client?.Name!;
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
        try
        {
            if(Challenger.Client == null || Challenged.Client == null)
            {
                SendMessage("Opponent has disconnected.");
                return;
            }

            if (LineUpRequired && !AreLinedUp())
            {
                SendMessage("You have 15 seconds to line up.");

                Task lineUpTimeout = Task.Delay(15000);
                Task lineUpChecker = Task.Run(async () =>
                {
                    while (!lineUpTimeout.IsCompleted && !AreLinedUp())
                        await Task.Delay(150);
                });

                Task completedTask = await Task.WhenAny(lineUpTimeout, lineUpChecker);
                if (completedTask == lineUpTimeout)
                {
                    SendMessage("You did not line up in time. The race has been cancelled.");
                    return;
                }
            }

            byte signalStage = 0;
            while(signalStage < 3)
            {
                if(!AreLinedUp())
                {
                    SendMessage("You went out of line. The race has been cancelled.");
                    return;
                }

                if (signalStage == 0)
                    _ = SendTimedMessageAsync("Ready...");
                else if (signalStage == 1)
                    _ = SendTimedMessageAsync("Set...");
                else if (signalStage == 2)
                {
                    _ = SendTimedMessageAsync("Go!");
                    break;
                }

                await Task.Delay(1000);
                signalStage++;
            }

            while (true)
            {
                if(Challenger.Client == null)
                {
                    Leader = Challenged;
                    Follower = Challenger;
                    return;
                }
                else if(Challenged.Client == null)
                {
                    Leader = Challenger;
                    Follower = Challenged;
                    return;
                }

                UpdateLeader();

                Vector3 leaderPosition = Leader.Status.Position;
                if (Vector3.DistanceSquared(LastLeaderPosition, leaderPosition) > 40000)
                {
                    Leader = Follower;
                    Follower = Leader;
                    return;
                }
                LastLeaderPosition = leaderPosition;

                if (Vector3.DistanceSquared(Leader.Status.Position, Follower!.Status.Position) > 562500)
                {
                    return;
                }

                if(_sessionManager.ServerTimeMilliseconds - LastOvertakeTime > 60000)
                    return;

                await Task.Delay(250);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while running race");
        }
        finally
        {
            FinishRace();
        }
    }

    [MemberNotNull(nameof(Leader))]
    private void UpdateLeader()
    {
        bool isFirstUpdate = false;
        if (Leader == null)
        {
            LastOvertakeTime = _sessionManager.ServerTimeMilliseconds;
            Leader = Challenger;
            Follower = Challenged;
            LastLeaderPosition = Leader.Status.Position;
            isFirstUpdate = true;
        }

        float challengerAngle = (float)(Math.Atan2(Challenged.Status.Position.X - Challenger.Status.Position.X, Challenged.Status.Position.Z - Challenger.Status.Position.Z) * 180 / Math.PI);
        if (challengerAngle < 0)
            challengerAngle += 360;
        float challengerRot = Challenger.Status.GetRotationAngle();

        challengerAngle += challengerRot;
        challengerAngle %= 360;

        float challengedAngle = (float)(Math.Atan2(Challenger.Status.Position.X - Challenged.Status.Position.X, Challenger.Status.Position.Z - Challenged.Status.Position.Z) * 180 / Math.PI);
        if (challengedAngle < 0)
            challengedAngle += 360;
        float challengedRot = Challenged.Status.GetRotationAngle();

        challengedAngle += challengedRot;
        challengedAngle %= 360;

        float challengerSpeed = (float)Math.Max(0.07716061728, Challenger.Status.Velocity.LengthSquared());
        float challengedSpeed = (float)Math.Max(0.07716061728, Challenged.Status.Velocity.LengthSquared());

        float distanceSquared = Vector3.DistanceSquared(Challenger.Status.Position, Challenged.Status.Position);

        EntryCar oldLeader = Leader;

        if ((challengerAngle > 90 && challengerAngle < 275) && Leader != Challenger && challengerSpeed > challengedSpeed && distanceSquared < 2500)
        {
            Leader = Challenger;
            Follower = Challenged;
        }
        else if ((challengedAngle > 90 && challengedAngle < 275) && Leader != Challenged && challengedSpeed > challengerSpeed && distanceSquared < 2500)
        {
            Leader = Challenged;
            Follower = Challenger;
        }

        if(oldLeader != Leader)
        {
            if (!isFirstUpdate)
                SendMessage($"{Leader.Client?.Name} has overtaken {oldLeader.Client?.Name}");

            LastOvertakeTime = _sessionManager.ServerTimeMilliseconds;
            LastLeaderPosition = Leader.Status.Position;
        }
    }

    private void FinishRace()
    {
        _plugin.GetRace(Challenger).CurrentRace = null;
        _plugin.GetRace(Challenged).CurrentRace = null;

        if (Leader != null)
        {
            string winnerName = Challenger == Leader ? ChallengerName : ChallengedName;
            string loserName = Challenger == Leader ? ChallengedName : ChallengerName;

            _entryCarManager.BroadcastChat($"{winnerName} just beat {loserName} in a race.");
            Log.Information("{WinnerName} just beat {LoserName} in a race", winnerName, loserName);
        }
    }

    private void SendMessage(string message)
    {
        Challenger.Client?.SendChatMessage(message);
        Challenged.Client?.SendChatMessage(message);
    }

    private bool AreLinedUp()
    {
        float distanceSquared = Vector3.DistanceSquared(Challenger.Status.Position, Challenged.Status.Position);

        if (!LineUpRequired)
        {
            return distanceSquared <= 900;
        }
        else
        {
            if (distanceSquared > 100)
                return false;
        }

        float angle = (float)(Math.Atan2(Challenged.Status.Position.X - Challenger.Status.Position.X, Challenged.Status.Position.Z - Challenger.Status.Position.Z) * 180 / Math.PI);
        if (angle < 0)
            angle += 360;
        float challengerRot = Challenger.Status.GetRotationAngle();

        angle += challengerRot;
        angle %= 360;
        
        if (!((angle <= 105 && angle >= 75) || (angle >= 255 && angle <= 285)))
            return false;

        angle = (float)(Math.Atan2(Challenger.Status.Position.X - Challenged.Status.Position.X, Challenger.Status.Position.Z - Challenged.Status.Position.Z) * 180 / Math.PI);
        if (angle < 0)
            angle += 360;
        float challengedRot = Challenged.Status.GetRotationAngle();

        angle += challengedRot;
        angle %= 360;
        
        if (!((angle <= 105 && angle >= 75) || (angle >= 255 && angle <= 285)))
            return false;

        float challengerDirection = Challenger.Status.GetRotationAngle();
        float challengedDirection = Challenged.Status.GetRotationAngle();

        float anglediff = (challengerDirection - challengedDirection + 180 + 360) % 360 - 180;
        if (Math.Abs(anglediff) > 5)
            return false;

        return true;
    }

    private async Task SendTimedMessageAsync(string message)
    {
        bool isChallengerHighPing = Challenger.Ping > Challenged.Ping;
        EntryCar highPingCar, lowPingCar;

        if(isChallengerHighPing)
        {
            highPingCar = Challenger;
            lowPingCar = Challenged;
        }
        else
        {
            highPingCar = Challenged;
            lowPingCar = Challenger;
        }

        highPingCar.Client?.SendChatMessage(message);
        await Task.Delay(highPingCar.Ping - lowPingCar.Ping);
        lowPingCar.Client?.SendChatMessage(message);
    }
}
