using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;
using System.Runtime.InteropServices;
using AssettoServer.Server.Ai;
using AssettoServer.Server.Ai.Splines;
using AssettoServer.Server.Configuration.Extra;
using AssettoServer.Shared.Model;
using AssettoServer.Shared.Network.Packets.Outgoing;

namespace AssettoServer.Server;

public enum AiMode
{
    None,
    Auto,
    Fixed
}

public partial class EntryCar
{
    public bool AiControlled { get; set; }
    public AiMode AiMode { get; set; }
    public int TargetAiStateCount { get; private set; } = 1;
    public byte[] LastSeenAiSpawn { get; }
    public byte[] AiPakSequenceIds { get; }
    public AiState?[] LastSeenAiState { get; }
    public string? AiName { get; private set; }
    public bool AiEnableColorChanges { get; set; } = false;
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
    
    private readonly Func<EntryCar, AiState> _aiStateFactory;
    private readonly AiSpline? _spline;

    private void AiInit()
    {
        AiName = $"{_configuration.Extra.AiParams.NamePrefix} {SessionId}";
        SetAiOverbooking(0);

        _configuration.Extra.AiParams.PropertyChanged += OnConfigReload;
        OnConfigReload(_configuration, new PropertyChangedEventArgs(string.Empty));
    }

    private void OnConfigReload(object? sender, PropertyChangedEventArgs args)
    {
        AiSplineHeightOffsetMeters = _configuration.Extra.AiParams.SplineHeightOffsetMeters;
        AiAcceleration = _configuration.Extra.AiParams.DefaultAcceleration;
        AiDeceleration = _configuration.Extra.AiParams.DefaultDeceleration;
        AiCorneringSpeedFactor = _configuration.Extra.AiParams.CorneringSpeedFactor;
        AiCorneringBrakeDistanceFactor = _configuration.Extra.AiParams.CorneringBrakeDistanceFactor;
        AiCorneringBrakeForceFactor = _configuration.Extra.AiParams.CorneringBrakeForceFactor;
        TyreDiameterMeters = _configuration.Extra.AiParams.TyreDiameterMeters;
        AiMinSpawnProtectionTimeMilliseconds = _configuration.Extra.AiParams.MinSpawnProtectionTimeMilliseconds;
        AiMaxSpawnProtectionTimeMilliseconds = _configuration.Extra.AiParams.MaxSpawnProtectionTimeMilliseconds;
        AiMinCollisionStopTimeMilliseconds = _configuration.Extra.AiParams.MinCollisionStopTimeMilliseconds;
        AiMaxCollisionStopTimeMilliseconds = _configuration.Extra.AiParams.MaxCollisionStopTimeMilliseconds;

        foreach (var carOverrides in _configuration.Extra.AiParams.CarSpecificOverrides)
        {
            if (carOverrides.Model == Model)
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
                    && Vector3.DistanceSquared(aiState.Status.Position, targetAiState.Status.Position) < _configuration.Extra.AiParams.MinStateDistanceSquared
                    && (_configuration.Extra.AiParams.TwoWayTraffic || Vector3.Dot(aiState.Status.Velocity, targetAiState.Status.Velocity) > 0))
                {
                    aiState.Despawn();
                    Logger.Verbose("Removed close state from AI {SessionId}", SessionId);
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

            if (_configuration.Extra.AiParams.TwoWayTraffic)
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
                bool isTieBreaker = minDistance < _configuration.Extra.AiParams.MinStateDistanceSquared &&
                                    distance < _configuration.Extra.AiParams.MinStateDistanceSquared &&
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
        ArgumentNullException.ThrowIfNull(_spline);

        var ops = _spline.Operations;
            
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

            Logger.Verbose("Removed state of Traffic {SessionId} due to overbooking reduction", SessionId);

            if (_aiStates.Count == 0)
            {
                Logger.Verbose("Traffic {SessionId} has no states left, disconnecting", SessionId);
                _entryCarManager.BroadcastPacket(new CarDisconnected { SessionId = SessionId });
            }

            return false;
        }

        foreach (var state in AiStatesSpan)
        {
            if (state == aiState || !state.Initialized) continue;

            if (Vector3.DistanceSquared(spawnPoint, state.Status.Position) < _configuration.Extra.AiParams.StateSpawnDistanceSquared)
            {
                return false;
            }
        }

        return true;
    }

    public void SetAiControl(bool aiControlled)
    {
        if (AiControlled != aiControlled)
        {
            AiControlled = aiControlled;

            if (AiControlled)
            {
                Logger.Debug("Slot {SessionId} is now controlled by AI", SessionId);

                AiReset();
                _entryCarManager.BroadcastPacket(new CarConnected
                {
                    SessionId = SessionId,
                    Name = AiName
                });
                if (_configuration.Extra.AiParams.HideAiCars)
                {
                    _entryCarManager.BroadcastPacket(new CSPCarVisibilityUpdate
                    {
                        SessionId = SessionId,
                        Visible = CSPCarVisibility.Invisible
                    });
                }
            }
            else
            {
                Logger.Debug("Slot {SessionId} is no longer controlled by AI", SessionId);
                if (_aiStates.Count > 0)
                {
                    _entryCarManager.BroadcastPacket(new CarDisconnected { SessionId = SessionId });
                }

                if (_configuration.Extra.AiParams.HideAiCars)
                {
                    _entryCarManager.BroadcastPacket(new CSPCarVisibilityUpdate
                    {
                        SessionId = SessionId,
                        Visible = CSPCarVisibility.Visible
                    });
                }

                AiReset();
            }
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
}
