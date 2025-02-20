using System.ComponentModel;
using System.Numerics;
using System.Runtime.InteropServices;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Shared.Model;
using AssettoServer.Shared.Network.Packets.Outgoing;
using AssettoServer.Shared.Network.Packets.Shared;
using Grpc.Core;
using TrafficAIPlugin.Configuration;
using TrafficAIPlugin.Splines;

namespace TrafficAIPlugin;

public class EntryCarTrafficAi
{
    public int TargetAiStateCount { get; private set; } = 1;
    public byte[] LastSeenAiSpawn { get; }
    public byte[] AiPakSequenceIds { get; }
    public AiState?[] LastSeenAiState { get; }
    public bool AiEnableColorChanges => EntryCar.DriverOptionsFlags.HasFlag(DriverOptionsFlags.AllowColorChange);
    public int AiIdleEngineRpm { get; set; } = 800;
    public int AiMaxEngineRpm { get; set; } = 3000;
    public float AiAcceleration { get; set; }
    public float AiDeceleration { get; set; }
    public float AiCorneringSpeedFactor { get; set; }
    public float AiCorneringBrakeDistanceFactor { get; set; }
    public float AiCorneringBrakeForceFactor { get; set; }
    public float AiSplineHeightOffsetMeters { get; set; }
    public int? AiMaxOverbooking { get; set; }
    public int AiMinSpawnProtectionTimeMilliseconds { get; set; }
    public int AiMaxSpawnProtectionTimeMilliseconds { get; set; }
    public int? MinLaneCount { get; set; }
    public int? MaxLaneCount { get; set; }
    public int AiMinCollisionStopTimeMilliseconds { get; set; }
    public int AiMaxCollisionStopTimeMilliseconds { get; set; }
    public float VehicleLengthPreMeters { get; set; }
    public float VehicleLengthPostMeters { get; set; }
    public int? MinAiSafetyDistanceMetersSquared { get; set; }
    public int? MaxAiSafetyDistanceMetersSquared { get; set; }
    public List<LaneSpawnBehavior>? AiAllowedLanes { get; set; }
    public float TyreDiameterMeters { get; set; }
    private readonly List<AiState> _aiStates = [];
    private Span<AiState> AiStatesSpan => CollectionsMarshal.AsSpan(_aiStates);
    
    private readonly Func<EntryCarTrafficAi, AiState> _aiStateFactory;
    private readonly TrafficAi _trafficAi;

    public readonly EntryCar EntryCar;
    
    private readonly ACServerConfiguration _serverConfiguration;
    private readonly TrafficAiConfiguration _configuration;
    private readonly EntryCarManager _entryCarManager;
    private readonly SessionManager _sessionManager;
    private readonly AiSpline _aiSpline;

    public EntryCarTrafficAi(EntryCar entryCar,
        TrafficAiConfiguration configuration,
        EntryCarManager entryCarManager,
        SessionManager sessionManager,
        ACServerConfiguration serverConfiguration,
        Func<EntryCarTrafficAi, AiState> aiStateFactory,
        TrafficAi trafficAi,
        AiSpline aiSpline)
    {
        EntryCar = entryCar;
        _configuration = configuration;
        _entryCarManager = entryCarManager;
        _sessionManager = sessionManager;
        _serverConfiguration = serverConfiguration;
        _aiStateFactory = aiStateFactory;
        _trafficAi = trafficAi;
        _aiSpline = aiSpline;
            
        
        AiPakSequenceIds = new byte[entryCarManager.EntryCars.Length];
        LastSeenAiState = new AiState[entryCarManager.EntryCars.Length];
        LastSeenAiSpawn = new byte[entryCarManager.EntryCars.Length];
        
        AiInit();
    }

    private void AiInit()
    {
        EntryCar.AiName = $"{_configuration.NamePrefix} {EntryCar.SessionId}";
        SetAiOverbooking(0);

        _configuration.PropertyChanged += OnConfigReload;
        OnConfigReload(_configuration, new PropertyChangedEventArgs(string.Empty));
    }

    private void OnConfigReload(object? sender, PropertyChangedEventArgs args)
    {
        AiSplineHeightOffsetMeters = _configuration.SplineHeightOffsetMeters;
        AiAcceleration = _configuration.DefaultAcceleration;
        AiDeceleration = _configuration.DefaultDeceleration;
        AiCorneringSpeedFactor = _configuration.CorneringSpeedFactor;
        AiCorneringBrakeDistanceFactor = _configuration.CorneringBrakeDistanceFactor;
        AiCorneringBrakeForceFactor = _configuration.CorneringBrakeForceFactor;
        TyreDiameterMeters = _configuration.TyreDiameterMeters;
        AiMinSpawnProtectionTimeMilliseconds = _configuration.MinSpawnProtectionTimeMilliseconds;
        AiMaxSpawnProtectionTimeMilliseconds = _configuration.MaxSpawnProtectionTimeMilliseconds;
        AiMinCollisionStopTimeMilliseconds = _configuration.MinCollisionStopTimeMilliseconds;
        AiMaxCollisionStopTimeMilliseconds = _configuration.MaxCollisionStopTimeMilliseconds;

        foreach (var carOverrides in _configuration.CarSpecificOverrides)
        {
            if (carOverrides.Model == EntryCar.Model)
            {
                if (carOverrides.SplineHeightOffsetMeters.HasValue)
                    AiSplineHeightOffsetMeters = carOverrides.SplineHeightOffsetMeters.Value;
                if (carOverrides.EngineIdleRpm.HasValue)
                    AiIdleEngineRpm = carOverrides.EngineIdleRpm.Value;
                if (carOverrides.EngineMaxRpm.HasValue)
                    AiMaxEngineRpm = carOverrides.EngineMaxRpm.Value;
                if (carOverrides.Acceleration.HasValue)
                    AiAcceleration = carOverrides.Acceleration.Value;
                if (carOverrides.Deceleration.HasValue)
                    AiDeceleration = carOverrides.Deceleration.Value;
                if (carOverrides.CorneringSpeedFactor.HasValue)
                    AiCorneringSpeedFactor = carOverrides.CorneringSpeedFactor.Value;
                if (carOverrides.CorneringBrakeDistanceFactor.HasValue)
                    AiCorneringBrakeDistanceFactor = carOverrides.CorneringBrakeDistanceFactor.Value;
                if (carOverrides.CorneringBrakeForceFactor.HasValue)
                    AiCorneringBrakeForceFactor = carOverrides.CorneringBrakeForceFactor.Value;
                if (carOverrides.TyreDiameterMeters.HasValue)
                    TyreDiameterMeters = carOverrides.TyreDiameterMeters.Value;
                if (carOverrides.MaxOverbooking.HasValue)
                    AiMaxOverbooking = carOverrides.MaxOverbooking.Value;
                if (carOverrides.MinSpawnProtectionTimeMilliseconds.HasValue)
                    AiMinSpawnProtectionTimeMilliseconds = carOverrides.MinSpawnProtectionTimeMilliseconds.Value;
                if (carOverrides.MaxSpawnProtectionTimeMilliseconds.HasValue)
                    AiMaxSpawnProtectionTimeMilliseconds = carOverrides.MaxSpawnProtectionTimeMilliseconds.Value;
                if (carOverrides.MinCollisionStopTimeMilliseconds.HasValue)
                    AiMinCollisionStopTimeMilliseconds = carOverrides.MinCollisionStopTimeMilliseconds.Value;
                if (carOverrides.MaxCollisionStopTimeMilliseconds.HasValue)
                    AiMaxCollisionStopTimeMilliseconds = carOverrides.MaxCollisionStopTimeMilliseconds.Value;
                if (carOverrides.VehicleLengthPreMeters.HasValue)
                    VehicleLengthPreMeters = carOverrides.VehicleLengthPreMeters.Value;
                if (carOverrides.VehicleLengthPostMeters.HasValue)
                    VehicleLengthPostMeters = carOverrides.VehicleLengthPostMeters.Value;
                
                AiAllowedLanes = carOverrides.AllowedLanes;
                MinAiSafetyDistanceMetersSquared = carOverrides.MinAiSafetyDistanceMetersSquared;
                MaxAiSafetyDistanceMetersSquared = carOverrides.MaxAiSafetyDistanceMetersSquared;
                MinLaneCount = carOverrides.MinLaneCount;
                MaxLaneCount = carOverrides.MaxLaneCount;
            }
        }
    }

    public void RemoveUnsafeStates()
    {
        foreach (var aiState in AiStatesSpan)
        {
            if (!aiState.Initialized) continue;

            foreach (var targetAiState in AiStatesSpan)
            {
                if (aiState != targetAiState
                    && targetAiState.Initialized
                    && Vector3.DistanceSquared(aiState.Status.Position, targetAiState.Status.Position) < _configuration.MinStateDistanceSquared
                    && (_configuration.TwoWayTraffic || Vector3.Dot(aiState.Status.Velocity, targetAiState.Status.Velocity) > 0))
                {
                    aiState.Despawn();
                    EntryCar.Logger.Verbose("Removed close state from AI {SessionId}", EntryCar.SessionId);
                }
            }
        }
    }

    public void AiUpdate()
    {
        foreach (var aiState in AiStatesSpan)
        {
            aiState.Update();
        }
    }

    public void AiObstacleDetection()
    {
        foreach (var aiState in AiStatesSpan)
        {
            aiState.DetectObstacles();
        }
    }

    public AiState? GetBestStateForPlayer(CarStatus playerStatus)
    {
        AiState? bestState = null;
        float minDistance = float.MaxValue;

        foreach (var aiState in AiStatesSpan)
        {
            if (!aiState.Initialized) continue;

            float distance = Vector3.DistanceSquared(aiState.Status.Position, playerStatus.Position);

            if (_configuration.TwoWayTraffic)
            {
                if (distance < minDistance)
                {
                    bestState = aiState;
                    minDistance = distance;
                }
            }
            else
            {
                bool isBestSameDirection = bestState != null && Vector3.Dot(bestState.Status.Velocity, playerStatus.Velocity) > 0;
                bool isCandidateSameDirection = Vector3.Dot(aiState.Status.Velocity, playerStatus.Velocity) > 0;
                bool isPlayerFastEnough = playerStatus.Velocity.LengthSquared() > 1;
                bool isTieBreaker = minDistance < _configuration.MinStateDistanceSquared &&
                                    distance < _configuration.MinStateDistanceSquared &&
                                    isPlayerFastEnough;

                // Tie breaker: Multiple close states, so take the one with min distance and same direction
                if ((isTieBreaker && isCandidateSameDirection && (distance < minDistance || !isBestSameDirection))
                    || (!isTieBreaker && distance < minDistance))
                {
                    bestState = aiState;
                    minDistance = distance;
                }
            }
        }

        return bestState;
    }

    public bool IsPositionSafe(int pointId)
    {
        ArgumentNullException.ThrowIfNull(_aiSpline);

        var ops = _aiSpline.Operations;
            
        foreach (var aiState in AiStatesSpan)
        {
            if (aiState.Initialized 
                && Vector3.DistanceSquared(aiState.Status.Position, ops.Points[pointId].Position) < aiState.SafetyDistanceSquared
                && ops.IsSameDirection(aiState.CurrentSplinePointId, pointId))
            {
                return false;
            }
        }

        return true;
    }

    public (AiState? AiState, float DistanceSquared) GetClosestAiState(Vector3 position)
    {
        AiState? closestState = null;
        float minDistanceSquared = float.MaxValue;
        
        foreach (var aiState in AiStatesSpan)
        {
            float distanceSquared = Vector3.DistanceSquared(position, aiState.Status.Position);
            if (distanceSquared < minDistanceSquared)
            {
                closestState = aiState;
                minDistanceSquared = distanceSquared;
            }
        }

        return (closestState, minDistanceSquared);
    }

    public void GetInitializedStates(List<AiState> initializedStates, List<AiState>? uninitializedStates = null)
    {
        foreach (var aiState in AiStatesSpan)
        {
            if (aiState.Initialized)
            {
                initializedStates.Add(aiState);
            }
            else
            {
                uninitializedStates?.Add(aiState);
            }
        }
    }
    
    public bool CanSpawnAiState(Vector3 spawnPoint, AiState aiState)
    {
        // Remove state if AI slot overbooking was reduced
        if (_aiStates.IndexOf(aiState) >= TargetAiStateCount)
        {
            aiState.Dispose();
            _aiStates.Remove(aiState);

            EntryCar.Logger.Verbose("Removed state of Traffic {SessionId} due to overbooking reduction", EntryCar.SessionId);

            if (_aiStates.Count == 0)
            {
                EntryCar.Logger.Verbose("Traffic {SessionId} has no states left, disconnecting", EntryCar.SessionId);
                _entryCarManager.BroadcastPacket(new CarDisconnected { SessionId = EntryCar.SessionId });
            }

            return false;
        }

        foreach (var state in AiStatesSpan)
        {
            if (state == aiState || !state.Initialized) continue;

            if (Vector3.DistanceSquared(spawnPoint, state.Status.Position) < _configuration.StateSpawnDistanceSquared)
            {
                return false;
            }
        }

        return true;
    }

    public void SetAiControl(bool aiControlled)
    {
        if (EntryCar.AiControlled == aiControlled) return;
        
        EntryCar.AiControlled = aiControlled;
        if (EntryCar.AiControlled)
        {
            EntryCar.Logger.Debug("Slot {SessionId} is now controlled by AI", EntryCar.SessionId);

            AiReset();
            _entryCarManager.BroadcastPacket(new CarConnected
            {
                SessionId = EntryCar.SessionId,
                Name = EntryCar.AiName
            });
            if (_configuration.HideAiCars)
            {
                _entryCarManager.BroadcastPacket(new CSPCarVisibilityUpdate
                {
                    SessionId = EntryCar.SessionId,
                    Visible = CSPCarVisibility.Invisible
                });
            }
        }
        else
        {
            EntryCar.Logger.Debug("Slot {SessionId} is no longer controlled by AI", EntryCar.SessionId);
            if (_aiStates.Count > 0)
            {
                _entryCarManager.BroadcastPacket(new CarDisconnected { SessionId = EntryCar.SessionId });
            }

            if (_configuration.HideAiCars)
            {
                _entryCarManager.BroadcastPacket(new CSPCarVisibilityUpdate
                {
                    SessionId = EntryCar.SessionId,
                    Visible = CSPCarVisibility.Visible
                });
            }

            AiReset();
        }
    }

    public void SetAiOverbooking(int count)
    {
        if (AiMaxOverbooking.HasValue)
        {
            count = Math.Min(count, AiMaxOverbooking.Value);
        }

        if (count > _aiStates.Count)
        {
            int newAis = count - _aiStates.Count;
            for (int i = 0; i < newAis; i++)
            {
                _aiStates.Add(_aiStateFactory(this));
            }
        }

        TargetAiStateCount = count;
    }

    private void AiReset()
    {
        foreach (var state in AiStatesSpan)
        {
            state.Despawn();
        }
        _aiStates.Clear();
        _aiStates.Add(_aiStateFactory(this));
    }

    public bool TryResetPosition()
    {
        if (_sessionManager.ServerTimeMilliseconds < _sessionManager.CurrentSession.StartTimeMilliseconds + 20_000 
            || (_sessionManager.ServerTimeMilliseconds > _sessionManager.CurrentSession.EndTimeMilliseconds
                && _sessionManager.CurrentSession.EndTimeMilliseconds > 0))
            return false;

        var (splinePointId, _) = _aiSpline.WorldToSpline(EntryCar.Status.Position);

        var splinePoint = _aiSpline.Points[splinePointId];
        
        var position = splinePoint.Position;
        var direction = - _aiSpline.Operations.GetForwardVector(splinePoint.NextId);
        
        EntryCar.Client?.SendCollisionUpdatePacket(false);
        
        _ = Task.Run(async () =>
        {
            await Task.Delay(500);
        
            EntryCar.Client?.SendTeleportCarPacket(position, direction);
            await Task.Delay(10000);
        
            EntryCar.Client?.SendCollisionUpdatePacket(true);
        });
    
        EntryCar.Logger.Information("Reset position for {Player} ({SessionId})",EntryCar.Client?.Name, EntryCar.Client?.SessionId);
        return true;
    }
    
    public bool GetPositionUpdateForCar(EntryCar toCar, out PositionUpdateOut positionUpdateOut)
    {
        CarStatus targetCarStatus;
        var toTargetCar = toCar.TargetCar;
        if (toTargetCar != null)
        {
            var toTargetCarAi = _trafficAi.GetAiCarBySessionId(toTargetCar.SessionId);
            if (toTargetCar.AiControlled && toTargetCarAi.LastSeenAiState[toCar.SessionId] != null)
            {
                targetCarStatus = toTargetCarAi.LastSeenAiState[toCar.SessionId]!.Status;
            }
            else
            {
                targetCarStatus = toTargetCar.Status;
            }
        }
        else
        {
            targetCarStatus = toCar.Status;
        }

        CarStatus status;
        if (EntryCar.AiControlled)
        {
            var aiState = GetBestStateForPlayer(targetCarStatus);

            if (aiState == null)
            {
                positionUpdateOut = default;
                return false;
            }

            if (LastSeenAiState[toCar.SessionId] != aiState
                || LastSeenAiSpawn[toCar.SessionId] != aiState.SpawnCounter)
            {
                LastSeenAiState[toCar.SessionId] = aiState;
                LastSeenAiSpawn[toCar.SessionId] = aiState.SpawnCounter;

                if (AiEnableColorChanges)
                {
                    toCar.Client?.SendPacket(new CSPCarColorUpdate
                    {
                        SessionId = EntryCar.SessionId,
                        Color = aiState.Color
                    });
                }
            }

            status = aiState.Status;
        }
        else
        {
            status = EntryCar.Status;
        }

        float distanceSquared = Vector3.DistanceSquared(status.Position, targetCarStatus.Position);
        if (EntryCar.TargetCar != null || distanceSquared > EntryCar.NetworkDistanceSquared)
        {
            if ((_sessionManager.ServerTimeMilliseconds - EntryCar.OtherCarsLastSentUpdateTime[toCar.SessionId]) < EntryCar.OutsideNetworkBubbleUpdateRateMs)
            {
                positionUpdateOut = default;
                return false;
            }

            EntryCar.OtherCarsLastSentUpdateTime[toCar.SessionId] = _sessionManager.ServerTimeMilliseconds;
        }

        positionUpdateOut = new PositionUpdateOut(EntryCar.SessionId,
            EntryCar.AiControlled ? AiPakSequenceIds[toCar.SessionId]++ : status.PakSequenceId,
            (uint)(status.Timestamp - toCar.TimeOffset),
            EntryCar.Ping,
            status.Position,
            status.Rotation,
            status.Velocity,
            status.TyreAngularSpeed[0],
            status.TyreAngularSpeed[1],
            status.TyreAngularSpeed[2],
            status.TyreAngularSpeed[3],
            status.SteerAngle,
            status.WheelAngle,
            status.EngineRpm,
            status.Gear,
            (_serverConfiguration.Extra.ForceLights || EntryCar.ForceLights)
                ? status.StatusFlag | CarStatusFlags.LightsOn
                : status.StatusFlag,
            status.PerformanceDelta,
            status.Gas);
        return true;
    }
}
