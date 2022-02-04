using System.Numerics;
using AssettoServer.Network.Packets.Incoming;
using AssettoServer.Network.Packets.Outgoing;
using AssettoServer.Network.Packets.Shared;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;

namespace RaceChallengePlugin;

internal class EntryCarRace
{
    private readonly ACServer _server;
    private readonly EntryCar _entryCar;
    
    public int LightFlashCount { get; internal set; }
    
    internal Race? CurrentRace { get; set; }

    private long LastLightFlashTime { get; set; }
    private long LastRaceChallengeTime { get; set; }

    internal EntryCarRace(EntryCar entryCar)
    {
        _server = entryCar.Server;
        _entryCar = entryCar;
        _entryCar.PositionUpdateReceived += OnPositionUpdateReceived;
        _entryCar.ResetInvoked += OnResetInvoked;
    }

    private void OnResetInvoked(EntryCar sender, EventArgs args)
    {
        CurrentRace = null;
    }

    private void OnPositionUpdateReceived(EntryCar sender, in PositionUpdateIn positionUpdate)
    {
        long currentTick = Environment.TickCount64;
        if(((_entryCar.Status.StatusFlag & CarStatusFlags.LightsOn) == 0 && (positionUpdate.StatusFlag & CarStatusFlags.LightsOn) != 0) || ((_entryCar.Status.StatusFlag & CarStatusFlags.HighBeamsOff) == 0 && (positionUpdate.StatusFlag & CarStatusFlags.HighBeamsOff) != 0))
        {
            LastLightFlashTime = currentTick;
            LightFlashCount++;
        }

        if ((_entryCar.Status.StatusFlag & CarStatusFlags.HazardsOn) == 0 && (positionUpdate.StatusFlag & CarStatusFlags.HazardsOn) != 0)
        {
            if (CurrentRace != null && !CurrentRace.HasStarted && !CurrentRace.LineUpRequired)
                _ = CurrentRace.StartAsync();
        }

        if (currentTick - LastLightFlashTime > 3000 && LightFlashCount > 0)
        {
            LightFlashCount = 0;
        }

        if (LightFlashCount == 3)
        {
            LightFlashCount = 0;

            if(currentTick - LastRaceChallengeTime > 20000)
            {
                Task.Run(ChallengeNearbyCar);
                LastRaceChallengeTime = currentTick;
            }
        }
    }

    internal void ChallengeCar(EntryCar car, bool lineUpRequired = true)
    {
        void Reply(string message)
            => _entryCar.Client?.SendPacket(new ChatMessage { SessionId = 255, Message = message });

        var currentRace = CurrentRace;
        if (currentRace != null)
        {
            if (currentRace.HasStarted)
                Reply("You are currently in a race.");
            else
                Reply("You have a pending race request.");
        }
        else
        {
            if (car == _entryCar)
                Reply("You cannot challenge yourself to a race.");
            else
            {
                currentRace = car.GetRace().CurrentRace;
                if (currentRace != null)
                {
                    if (currentRace.HasStarted)
                        Reply("This car is currently in a race.");
                    else
                        Reply("This car has a pending race request.");
                }
                else
                {
                    currentRace = new Race(_server, _entryCar, car, lineUpRequired);
                    CurrentRace = currentRace;
                    car.GetRace().CurrentRace = currentRace;

                    _entryCar.Client?.SendPacket(new ChatMessage { SessionId = 255, Message = $"You have challenged {car.Client?.Name} to a race." });

                    if (lineUpRequired)
                        car.Client?.SendPacket(new ChatMessage { SessionId = 255, Message = $"{_entryCar.Client?.Name} has challenged you to a race. Send /accept within 10 seconds to accept." });
                    else
                        car.Client?.SendPacket(new ChatMessage
                            { SessionId = 255, Message = $"{_entryCar.Client?.Name} has challenged you to a race. Flash your hazard lights or send /accept within 10 seconds to accept." });

                    _ = Task.Delay(10000).ContinueWith(t =>
                    {
                        if (!currentRace.HasStarted)
                        {
                            CurrentRace = null;
                            car.GetRace().CurrentRace = null;

                            ChatMessage timeoutMessage = new ChatMessage { SessionId = 255, Message = $"Race request has timed out." };
                            _entryCar.Client?.SendPacket(timeoutMessage);
                            car.Client?.SendPacket(timeoutMessage);
                        }
                    });
                }
            }
        }
    }

    private void ChallengeNearbyCar()
    {
        EntryCar? bestMatch = null;
        float distanceSquared = 30 * 30;

        foreach(EntryCar car in _server.EntryCars)
        {
            ACTcpClient? carClient = car.Client;
            if(carClient != null && car != _entryCar)
            {
                float challengedAngle = (float)(Math.Atan2(_entryCar.Status.Position.X - car.Status.Position.X, _entryCar.Status.Position.Z - car.Status.Position.Z) * 180 / Math.PI);
                if (challengedAngle < 0)
                    challengedAngle += 360;
                float challengedRot = car.Status.GetRotationAngle();

                challengedAngle += challengedRot;
                challengedAngle %= 360;

                if (challengedAngle > 110 && challengedAngle < 250 && Vector3.DistanceSquared(car.Status.Position, _entryCar.Status.Position) < distanceSquared)
                    bestMatch = car;
            }
        }

        if (bestMatch != null)
            ChallengeCar(bestMatch, false);
    }
}