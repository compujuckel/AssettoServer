using System.Numerics;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Shared.Network.Packets.Incoming;
using AssettoServer.Shared.Network.Packets.Outgoing;
using AssettoServer.Shared.Network.Packets.Shared;

namespace RaceChallengePlugin;

public class EntryCarRace
{
    private readonly SessionManager _sessionManager;
    private readonly EntryCarManager _entryCarManager;
    private readonly RaceChallengePlugin _plugin;
    private readonly EntryCar _entryCar;
    private readonly Race.Factory _raceFactory;
    
    public int LightFlashCount { get; internal set; }
    
    internal Race? CurrentRace { get; set; }

    private long LastLightFlashTime { get; set; }
    private long LastRaceChallengeTime { get; set; }

    public EntryCarRace(EntryCar entryCar, SessionManager sessionManager, EntryCarManager entryCarManager, RaceChallengePlugin plugin, Race.Factory raceFactory)
    {
        _entryCar = entryCar;
        _sessionManager = sessionManager;
        _entryCarManager = entryCarManager;
        _plugin = plugin;
        _raceFactory = raceFactory;
        _entryCar.PositionUpdateReceived += OnPositionUpdateReceived;
        _entryCar.ResetInvoked += OnResetInvoked;
    }

    private void OnResetInvoked(EntryCar sender, EventArgs args)
    {
        CurrentRace = null;
    }

    private void OnPositionUpdateReceived(EntryCar sender, in PositionUpdateIn positionUpdate)
    {
        long currentTick = _sessionManager.ServerTimeMilliseconds;
        if(((_entryCar.Status.StatusFlag & CarStatusFlags.LightsOn) == 0 && (positionUpdate.StatusFlag & CarStatusFlags.LightsOn) != 0) 
           || ((_entryCar.Status.StatusFlag & CarStatusFlags.HighBeamsOff) == 0 && (positionUpdate.StatusFlag & CarStatusFlags.HighBeamsOff) != 0))
        {
            LastLightFlashTime = currentTick;
            LightFlashCount++;
        }

        if ((_entryCar.Status.StatusFlag & CarStatusFlags.HazardsOn) == 0 
            && (positionUpdate.StatusFlag & CarStatusFlags.HazardsOn) != 0
            && CurrentRace != null 
            && CurrentRace.Challenged == sender 
            && !CurrentRace.HasStarted 
            && !CurrentRace.LineUpRequired)
        {
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
            => _entryCar.Client?.SendChatMessage(message);

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
                currentRace = _plugin.GetRace(car).CurrentRace;
                if (currentRace != null)
                {
                    if (currentRace.HasStarted)
                        Reply("This car is currently in a race.");
                    else
                        Reply("This car has a pending race request.");
                }
                else
                {
                    currentRace = _raceFactory(_entryCar, car, lineUpRequired);
                    CurrentRace = currentRace;
                    _plugin.GetRace(car).CurrentRace = currentRace;

                    _entryCar.Client?.SendChatMessage($"You have challenged {car.Client?.Name} to a race.");

                    if (lineUpRequired)
                        car.Client?.SendChatMessage($"{_entryCar.Client?.Name} has challenged you to a race. Send /accept within 10 seconds to accept.");
                    else
                        car.Client?.SendChatMessage($"{_entryCar.Client?.Name} has challenged you to a race. Flash your hazard lights or send /accept within 10 seconds to accept.");

                    _ = Task.Delay(10000).ContinueWith(_ =>
                    {
                        if (!currentRace.HasStarted)
                        {
                            CurrentRace = null;
                            _plugin.GetRace(car).CurrentRace = null;

                            var timeoutMessage = "Race request has timed out.";
                            _entryCar.Client?.SendChatMessage(timeoutMessage);
                            car.Client?.SendChatMessage(timeoutMessage);
                        }
                    });
                }
            }
        }
    }

    private void ChallengeNearbyCar()
    {
        EntryCar? bestMatch = null;
        const float distanceSquared = 30 * 30;

        foreach(EntryCar car in _entryCarManager.EntryCars)
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
