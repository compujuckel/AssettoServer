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
    private int ChallengerPoints { get; set; }
    private int ChallengedPoints { get; set; }

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
            
            Challenger.Client.SendPacket(new RaceChallengeStatus
            {
                EventType = RaceChallengeEvent.RaceChallenge,
                EventData = Challenged.SessionId
            });
            Challenged.Client.SendPacket(new RaceChallengeStatus
            {
                EventType = RaceChallengeEvent.RaceChallenge,
                EventData = Challenger.SessionId
            });

            await Task.Delay(500);

            var countdownPacket = new RaceChallengeStatus
            {
                EventType = RaceChallengeEvent.RaceCountdown,
                EventData = Server.CurrentTime + 3000
            };
            Challenger.Client.SendPacket(countdownPacket);
            Challenged.Client.SendPacket(countdownPacket);

            await Task.Delay(3000);
            
            if (!AreLinedUp())
            {
                SendMessage("You went out of line. The race has been cancelled.");

                var endPacket = new RaceChallengeStatus
                {
                    EventType = RaceChallengeEvent.RaceEnded,
                    EventData = 255
                };
                Challenger.Client.SendPacket(endPacket);
                Challenged.Client.SendPacket(endPacket);

                return;
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

                if (Server.CurrentTime64 - LastPointCheckTime >= 250)
                {
                    float dt = (Server.CurrentTime64 - LastPointCheckTime) / 1000.0f;
                    LastPointCheckTime = Server.CurrentTime64;
                    
                    float distanceBetweenPlayersSquared = Vector3.DistanceSquared(leaderPosition, followerPosition);
                    float pointsToBeSubtractedFromFollower = 0.001f * distanceBetweenPlayersSquared + 8f;

                    float challengerRate = 0;
                    float challengedRate = 0;

                    if (Follower == Challenger)
                    {
                        ChallengerPoints -= (int)(pointsToBeSubtractedFromFollower * dt);
                        challengerRate = -pointsToBeSubtractedFromFollower;
                    }
                    else
                    {
                        ChallengedPoints -= (int)(pointsToBeSubtractedFromFollower * dt);
                        challengedRate = -pointsToBeSubtractedFromFollower;
                    }

                    Challenger.Client.SendPacket(new RaceChallengeUpdate
                    {
                        OwnHealth = (float)ChallengerPoints / 3000,
                        OwnRate = challengerRate / 3000,
                        RivalHealth = (float)ChallengedPoints / 3000,
                        RivalRate = challengedRate / 3000
                    });
                    
                    Challenged.Client.SendPacket(new RaceChallengeUpdate
                    {
                        OwnHealth = (float)ChallengedPoints / 3000,
                        OwnRate = challengedRate / 3000,
                        RivalHealth = (float)ChallengerPoints / 3000,
                        RivalRate = challengerRate / 3000
                    });
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

        //if (Leader == null) return;

        string winnerName = Challenger == Leader ? ChallengerName : ChallengedName;
        string loserName = Challenger == Leader ? ChallengedName : ChallengerName;

        var endPacket = new RaceChallengeStatus
        {
            EventType = RaceChallengeEvent.RaceEnded,
            EventData = Leader.SessionId
        };
        Challenger.Client?.SendPacket(endPacket);
        Challenged.Client?.SendPacket(endPacket);

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
