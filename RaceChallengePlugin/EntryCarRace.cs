using System.Numerics;
using AssettoServer.Network.Packets.Shared;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;

namespace RaceChallengePlugin;

internal class EntryCarRace
{
    private readonly ACServer _server;
    private readonly EntryCar _entryCar;

    private int LightFlashCount { get; set; }
    private long LastLightFlashTime { get; set; }
    private long LastRaceChallengeTime { get; set; }
    internal Race CurrentRace { get; set; }

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

    private void OnPositionUpdateReceived(EntryCar sender, PositionUpdateEventArgs args)
    {
        long currentTick = Environment.TickCount64;
        if ((_entryCar.Status.StatusFlag & CarStatusFlags.LightsOn) == 0 && (args.PositionUpdate.StatusFlag & CarStatusFlags.LightsOn) != 0 ||
            (_entryCar.Status.StatusFlag & CarStatusFlags.HighBeamsOff) == 0 && (args.PositionUpdate.StatusFlag & CarStatusFlags.HighBeamsOff) != 0)
        {
            LastLightFlashTime = currentTick;
            LightFlashCount++;
        }

        if ((_entryCar.Status.StatusFlag & CarStatusFlags.HazardsOn) == 0 && (args.PositionUpdate.StatusFlag & CarStatusFlags.HazardsOn) != 0 &&
            CurrentRace is {HasStarted: false, LineUpRequired: false})
            _ = CurrentRace.StartAsync();

        if (currentTick - LastLightFlashTime > 3000 && LightFlashCount > 0)
            LightFlashCount = 0;

        if (LightFlashCount != 3) return;
        LightFlashCount = 0;

        if (currentTick - LastRaceChallengeTime <= 20000) return;
        Task.Run(ChallengeNearbyCar);
        LastRaceChallengeTime = currentTick;
    }

    internal void ChallengeCar(EntryCar car, bool lineUpRequired = true)
    {
        void Reply(string message)
            => _entryCar.Client.SendPacket(new ChatMessage {SessionId = 255, Message = message});

        Race currentRace = CurrentRace;
        if (currentRace != null)
        {
            Reply(currentRace.HasStarted ? "You are currently in a race." : "You have a pending race request.");
            return;
        }

        if (car == _entryCar)
        {
            Reply("You cannot challenge yourself to a race.");
            return;
        }

        currentRace = car.GetRace().CurrentRace;
        if (currentRace != null)
        {
            Reply(currentRace.HasStarted ? "This car is currently in a race." : "This car has a pending race request.");
            return;
        }

        currentRace = new Race(_server, _entryCar, car, lineUpRequired);
        CurrentRace = currentRace;
        car.GetRace().CurrentRace = currentRace;

        _entryCar.Client.SendPacket(new ChatMessage {SessionId = 255, Message = $"You have challenged {car.Client.Name} to a race."});

        if (lineUpRequired)
            car.Client.SendPacket(new ChatMessage {SessionId = 255, Message = $"{_entryCar.Client.Name} has challenged you to a race. Send /accept within 10 seconds to accept."});
        else
            car.Client.SendPacket(new ChatMessage
                {SessionId = 255, Message = $"{_entryCar.Client.Name} has challenged you to a race. Flash your hazard lights or send /accept within 10 seconds to accept."});

        _ = Task.Delay(10000).ContinueWith(_ =>
        {
            if (currentRace.HasStarted) return;
            CurrentRace = null;
            car.GetRace().CurrentRace = null;

            ChatMessage timeoutMessage = new ChatMessage {SessionId = 255, Message = "Race request has timed out."};
            _entryCar.Client.SendPacket(timeoutMessage);
            car.Client.SendPacket(timeoutMessage);
        });
    }

    private void ChallengeNearbyCar()
    {
        EntryCar bestMatch = null;
        const float distanceSquared = 30 * 30;

        foreach (EntryCar car in _server.EntryCars)
        {
            ACTcpClient carClient = car.Client;
            if (carClient == null || car == _entryCar) continue;

            float challengedAngle = (float) (Math.Atan2(_entryCar.Status.Position.X - car.Status.Position.X, _entryCar.Status.Position.Z - car.Status.Position.Z) * 180 / Math.PI);
            if (challengedAngle < 0)
                challengedAngle += 360;
            float challengedRot = car.Status.GetRotationAngle();

            challengedAngle += challengedRot;
            challengedAngle %= 360;

            if (challengedAngle is > 110 and < 250 && Vector3.DistanceSquared(car.Status.Position, _entryCar.Status.Position) < distanceSquared)
                bestMatch = car;
        }

        if (bestMatch != null)
            ChallengeCar(bestMatch, false);
    }
}