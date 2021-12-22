using System.Numerics;
using AssettoServer.Network.Packets.Shared;
using AssettoServer.Server;
using Serilog;

namespace RaceChallengePlugin;

public class Race
{
    private ACServer Server { get; }
    private EntryCar Challenger { get; }
    private EntryCar Challenged { get; }
    private EntryCar Leader { get; set; }
    private EntryCar Follower { get; set; }

    public bool HasStarted { get; private set; }
    public bool LineUpRequired { get; }

    private long LastOvertakeTime { get; set; }
    private Vector3 LastLeaderPosition { get; set; }
    private string ChallengerName { get; }
    private string ChallengedName { get; }
    private long LastPointCheckTime { get; set; }
    private long LastPointBroadcastTime { get; set; }
    private long ChallengerPoints { get; set; }
    private long ChallengedPoints { get; set; }

    public Race(ACServer server, EntryCar challenger, EntryCar challenged, bool lineUpRequired = true)
    {
        Server = server;
        Challenger = challenger;
        ChallengerPoints = 3000;
        Challenged = challenged;
        ChallengedPoints = 3000;
        LineUpRequired = lineUpRequired;

        ChallengerName = Challenger.Client.Name;
        ChallengedName = Challenged.Client.Name;
    }

    public Task StartAsync()
    {
        if (HasStarted) return Task.CompletedTask;

        HasStarted = true;
        _ = Task.Run(RaceAsync);
        return Task.CompletedTask;
    }

    private async Task RaceAsync()
    {
        LastPointCheckTime = Server.CurrentTime64;
        LastPointBroadcastTime = Server.CurrentTime64;
        try
        {
            if (Challenger.Client == null || Challenged.Client == null)
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
            while (signalStage < 4)
            {
                if (!AreLinedUp())
                {
                    SendMessage("You went out of line. The race has been cancelled.");
                    return;
                }

                if (signalStage == 0)
                    _ = SendTimedMessageAsync("3...");
                else if (signalStage == 1)
                    _ = SendTimedMessageAsync("2...");
                else if (signalStage == 2)
                    _ = SendTimedMessageAsync("1...");
                else if (signalStage == 3)
                {
                    _ = SendTimedMessageAsync("Go!");
                    break;
                }

                await Task.Delay(1000);
                signalStage++;
            }

            while (true)
            {
                if (Challenger.Client == null)
                {
                    Leader = Challenged;
                    Follower = Challenger;
                    return;
                }
                else if (Challenged.Client == null)
                {
                    Leader = Challenger;
                    Follower = Challenged;
                    return;
                }

                UpdateLeader();

                Vector3 leaderPosition = Leader.Status.Position;
                Vector3 followerPosition = Follower.Status.Position;
                if (Vector3.DistanceSquared(LastLeaderPosition, leaderPosition) > 40000)
                {
                    Leader = Follower;
                    Follower = Leader;
                    return;
                }

                LastLeaderPosition = leaderPosition;

                if (Server.CurrentTime64 - LastPointCheckTime >= 1000)
                {
                    var distanceBetweenPlayersSquared = Vector3.DistanceSquared(leaderPosition, followerPosition);
                    long pointsToBeSubtractedFromFollower = distanceBetweenPlayersSquared switch
                    {
                        <= 75 * 75 => 2,
                        > 75 * 75 and <= 300 * 300 => 50,
                        > 300 * 300 and <= 500 * 500 => 100,
                        _ => 1000
                    };

                    if (Follower == Challenger)
                        ChallengerPoints -= pointsToBeSubtractedFromFollower;
                    else
                        ChallengedPoints -= pointsToBeSubtractedFromFollower;

                    LastPointCheckTime = Server.CurrentTime64;
                }

                if (Server.CurrentTime64 - LastPointBroadcastTime >= 10000)
                {
                    SendMessage(ChallengedName + " points: " + ChallengedPoints);
                    SendMessage(ChallengerName + " points: " + ChallengerPoints);
                    LastPointBroadcastTime = Server.CurrentTime64;
                }

                if (ChallengerPoints <= 0 || ChallengedPoints <= 0)
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

    private void UpdateLeader()
    {
        bool isFirstUpdate = false;
        if (Leader == null)
        {
            LastOvertakeTime = Server.CurrentTime64;
            Leader = Challenger;
            Follower = Challenged;
            LastLeaderPosition = Leader.Status.Position;
            isFirstUpdate = true;
        }

        float challengerAngle = (float) (Math.Atan2(Challenged.Status.Position.X - Challenger.Status.Position.X, Challenged.Status.Position.Z - Challenger.Status.Position.Z) * 180 / Math.PI);
        if (challengerAngle < 0)
            challengerAngle += 360;
        float challengerRot = Challenger.Status.GetRotationAngle();

        challengerAngle += challengerRot;
        challengerAngle %= 360;

        float challengedAngle = (float) (Math.Atan2(Challenger.Status.Position.X - Challenged.Status.Position.X, Challenger.Status.Position.Z - Challenged.Status.Position.Z) * 180 / Math.PI);
        if (challengedAngle < 0)
            challengedAngle += 360;
        float challengedRot = Challenged.Status.GetRotationAngle();

        challengedAngle += challengedRot;
        challengedAngle %= 360;

        float challengerSpeed = (float) Math.Max(0.07716061728, Challenger.Status.Velocity.LengthSquared());
        float challengedSpeed = (float) Math.Max(0.07716061728, Challenged.Status.Velocity.LengthSquared());

        float distanceSquared = Vector3.DistanceSquared(Challenger.Status.Position, Challenged.Status.Position);

        EntryCar oldLeader = Leader;

        if (challengerAngle is > 90 and < 275 && Leader != Challenger && challengerSpeed > challengedSpeed && distanceSquared < 2500)
        {
            Leader = Challenger;
            Follower = Challenged;
        }
        else if (challengedAngle is > 90 and < 275 && Leader != Challenged && challengedSpeed > challengerSpeed && distanceSquared < 2500)
        {
            Leader = Challenged;
            Follower = Challenger;
        }

        if (oldLeader == Leader) return;

        if (!isFirstUpdate)
            SendMessage($"{Leader.Client?.Name} has overtaken {oldLeader.Client?.Name}");

        LastOvertakeTime = Server.CurrentTime64;
        LastLeaderPosition = Leader.Status.Position;
    }

    private void FinishRace()
    {
        Challenger.GetRace().CurrentRace = null;
        Challenged.GetRace().CurrentRace = null;
        Log.Information("Ended race between {0} and {1}", ChallengerName, ChallengedName);

        if (Leader == null) return;

        string winnerName = Challenger == Leader ? ChallengerName : ChallengedName;
        string loserName = Challenger == Leader ? ChallengedName : ChallengerName;

        Server.BroadcastPacket(new ChatMessage {SessionId = 255, Message = $"{winnerName} just beat {loserName} in a race."});
    }

    private void SendMessage(string message)
    {
        if (Challenger.Client != null)
            SendMessage(Challenger, message);

        if (Challenged.Client != null)
            SendMessage(Challenged, message);
    }

    private bool AreLinedUp()
    {
        float distanceSquared = Vector3.DistanceSquared(Challenger.Status.Position, Challenged.Status.Position);
        Console.WriteLine("Distance: {0}", Math.Sqrt(distanceSquared));

        if (!LineUpRequired)
            return distanceSquared <= 900;

        if (distanceSquared > 100)
            return false;

        float angle = (float) (Math.Atan2(Challenged.Status.Position.X - Challenger.Status.Position.X, Challenged.Status.Position.Z - Challenger.Status.Position.Z) * 180 / Math.PI);
        if (angle < 0)
            angle += 360;
        float challengerRot = Challenger.Status.GetRotationAngle();

        angle += challengerRot;
        angle %= 360;

        Console.WriteLine("Challenger angle: {0}", angle);
        if (angle is not (<= 105 and >= 75 or >= 255 and <= 285))
            return false;

        angle = (float) (Math.Atan2(Challenger.Status.Position.X - Challenged.Status.Position.X, Challenger.Status.Position.Z - Challenged.Status.Position.Z) * 180 / Math.PI);
        if (angle < 0)
            angle += 360;
        float challengedRot = Challenged.Status.GetRotationAngle();

        angle += challengedRot;
        angle %= 360;

        Console.WriteLine("Challenged angle: {0}", angle);
        if (angle is not (<= 105 and >= 75 or >= 255 and <= 285))
            return false;

        float challengerDirection = Challenger.Status.GetRotationAngle();
        float challengedDirection = Challenged.Status.GetRotationAngle();

        float angleDiff = (challengerDirection - challengedDirection + 180 + 360) % 360 - 180;
        Console.WriteLine("Direction difference: {0}", angleDiff);

        return !(Math.Abs(angleDiff) > 5);
    }

    private static void SendMessage(EntryCar car, string message)
    {
        car.Client?.SendPacket(new ChatMessage {SessionId = 255, Message = message});
    }

    private async Task SendTimedMessageAsync(string message)
    {
        bool isChallengerHighPing = Challenger.Ping > Challenged.Ping;
        EntryCar highPingCar, lowPingCar;

        if (isChallengerHighPing)
        {
            highPingCar = Challenger;
            lowPingCar = Challenged;
        }
        else
        {
            highPingCar = Challenged;
            lowPingCar = Challenger;
        }

        SendMessage(highPingCar, message);
        await Task.Delay(highPingCar.Ping - lowPingCar.Ping);
        SendMessage(lowPingCar, message);
    }
}