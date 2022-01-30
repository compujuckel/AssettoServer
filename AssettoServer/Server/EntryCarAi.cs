using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Threading;
using AssettoServer.Network.Packets.Outgoing;
using AssettoServer.Server.Ai;
using Serilog;

namespace AssettoServer.Server;

public enum AiMode
{
    Disabled,
    Auto,
    Fixed
}

public partial class EntryCar
{
    public bool AiControlled { get; set; }
    public AiMode AiMode { get; init; }
    public int TargetAiStateCount { get; private set; } = 1;
    [NotNull] public byte[]? LastSeenAiSpawn { get; init; }
    [NotNull] public byte[]? AiPakSequenceIds { get; init; }
    [NotNull] public AiState?[]? LastSeenAiState { get; init; }
    public string? AiName { get; private set; }
    public bool AiEnableColorChanges { get; set; } = false;
    public int AiIdleEngineRpm { get; set; } = 800;
    public int AiMaxEngineRpm { get; set; } = 3000;
    public float AiSplineHeightOffsetMeters { get; set; } = 0;
    
    private readonly List<AiState> _aiStates = new List<AiState>();
    private readonly ReaderWriterLockSlim _aiStatesLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

    public void AiInit()
    {
        AiName = $"{Server.Configuration.Extra.AiParams.NamePrefix} {SessionId}";
        SetAiOverbooking(0);
    }
    
    public List<AiState> GetAiStatesCopy()
    {
        _aiStatesLock.EnterReadLock();
        try
        {
            return new List<AiState>(_aiStates);
        }
        finally
        {
            _aiStatesLock.ExitReadLock();
        }
    }

    public int GetActiveAiStateCount()
    {
        if (!AiControlled) return 0;
        
        _aiStatesLock.EnterReadLock();
        try
        {
            return _aiStates.Count(aiState => aiState.Initialized);
        }
        finally
        {
            _aiStatesLock.ExitReadLock();
        }
    }

    public void RemoveUnsafeStates()
    {
        _aiStatesLock.EnterReadLock();
        try
        {
            foreach (var aiState in _aiStates)
            {
                if (!aiState.Initialized) continue;

                foreach (var targetAiState in _aiStates)
                {
                    if (aiState != targetAiState
                        && targetAiState.Initialized
                        && Vector3.DistanceSquared(aiState.Status.Position, targetAiState.Status.Position) < Server.Configuration.Extra.AiParams.MinStateDistanceSquared
                        && Vector3.Dot(aiState.Status.Velocity, targetAiState.Status.Velocity) > 0)
                    {
                        aiState.Initialized = false;
                        Logger.Debug("Removed close state from AI {SessionId}", SessionId);
                    }
                }
            }
        }
        finally
        {
            _aiStatesLock.ExitReadLock();
        }
    }

    public void AiUpdate()
    {
        _aiStatesLock.EnterReadLock();
        try
        {
            foreach (var aiState in _aiStates)
            {
                aiState.Update();
            }
        }
        finally
        {
            _aiStatesLock.ExitReadLock();
        }
    }

    public void AiObstacleDetection()
    {
        _aiStatesLock.EnterReadLock();
        try
        {
            foreach (var aiState in _aiStates)
            {
                aiState.DetectObstacles();
            }
        }
        finally
        {
            _aiStatesLock.ExitReadLock();
        }
    }

    public AiState? GetBestStateForPlayer(CarStatus playerStatus)
    {
        _aiStatesLock.EnterReadLock();
        try
        {
            AiState? bestState = null;
            float minDistance = float.MaxValue;

            foreach (var aiState in _aiStates)
            {
                if (!aiState.Initialized) continue;

                float distance = Vector3.DistanceSquared(aiState.Status.Position, playerStatus.Position);
                bool isBestSameDirection = bestState != null && Vector3.Dot(bestState.Status.Velocity, playerStatus.Velocity) > 0;
                bool isCandidateSameDirection = Vector3.Dot(aiState.Status.Velocity, playerStatus.Velocity) > 0;
                bool isPlayerFastEnough = playerStatus.Velocity.LengthSquared() > 1;
                bool isTieBreaker = minDistance < Server.Configuration.Extra.AiParams.StateTieBreakerDistanceSquared &&
                                    distance < Server.Configuration.Extra.AiParams.StateTieBreakerDistanceSquared &&
                                    isPlayerFastEnough;

                // Tie breaker: Multiple close states, so take the one with min distance and same direction
                if ((isTieBreaker && isCandidateSameDirection && (distance < minDistance || !isBestSameDirection))
                    || (!isTieBreaker && distance < minDistance))
                {
                    bestState = aiState;
                    minDistance = distance;
                }
            }

            return bestState;
        }
        finally
        {
            _aiStatesLock.ExitReadLock();
        }
    }

    public bool IsPositionSafe(Vector3 position)
    {
        _aiStatesLock.EnterReadLock();
        try
        {
            foreach (var aiState in _aiStates)
            {
                if (aiState.Initialized && Vector3.DistanceSquared(aiState.Status.Position, position) < aiState.SafetyDistanceSquared)
                {
                    return false;
                }
            }
        }
        finally
        {
            _aiStatesLock.ExitReadLock();
        }

        return true;
    }

    public (AiState aiState, float distanceSquared) GetClosestAiState(Vector3 position)
    {
        AiState? closestState = null;
        float minDistanceSquared = float.MaxValue;

        _aiStatesLock.EnterReadLock();
        try
        {
            foreach (var aiState in _aiStates)
            {
                float distanceSquared = Vector3.DistanceSquared(position, aiState.Status.Position);
                if (distanceSquared < minDistanceSquared)
                {
                    closestState = aiState;
                    minDistanceSquared = distanceSquared;
                }
            }
        }
        finally
        {
            _aiStatesLock.ExitReadLock();
        }

        if (closestState == null)
            throw new InvalidOperationException("Could not get closest AI state");

        return (closestState, minDistanceSquared);
    }
    
    public bool CanSpawnAiState(Vector3 spawnPoint, AiState aiState)
    {
        _aiStatesLock.EnterUpgradeableReadLock();
        try
        {
            // Remove state if AI slot overbooking was reduced
            if (_aiStates.IndexOf(aiState) >= TargetAiStateCount)
            {
                _aiStatesLock.EnterWriteLock();
                try
                {
                    _aiStates.Remove(aiState);
                }
                finally
                {
                    _aiStatesLock.ExitWriteLock();
                }

                Logger.Verbose("Removed state of Traffic {SessionId} due to overbooking reduction", SessionId);

                if (_aiStates.Count == 0)
                {
                    Logger.Verbose("Traffic {SessionId} has no states left, disconnecting", SessionId);
                    Server.BroadcastPacket(new CarDisconnected { SessionId = SessionId });
                }

                return false;
            }

            foreach (var state in _aiStates)
            {
                if (state == aiState || !state.Initialized) continue;

                if (Vector3.DistanceSquared(spawnPoint, state.Status.Position) < Server.Configuration.Extra.AiParams.StateSpawnDistanceSquared)
                {
                    return false;
                }
            }

            return true;
        }
        finally
        {
            _aiStatesLock.ExitUpgradeableReadLock();
        }
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
                Server.BroadcastPacket(new CarConnected
                {
                    SessionId = SessionId,
                    Name = AiName
                });
                if (Server.Configuration.Extra.AiParams.HideAiCars)
                {
                    Server.BroadcastPacket(new CSPCarVisibilityUpdate
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
                    Server.BroadcastPacket(new CarDisconnected { SessionId = SessionId });
                }

                if (Server.Configuration.Extra.AiParams.HideAiCars)
                {
                    Server.BroadcastPacket(new CSPCarVisibilityUpdate
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
        _aiStatesLock.EnterUpgradeableReadLock();
        try
        {
            if (count > _aiStates.Count)
            {
                _aiStatesLock.EnterWriteLock();
                try
                {
                    int newAis = count - _aiStates.Count;
                    for (int i = 0; i < newAis; i++)
                    {
                        _aiStates.Add(new AiState(this));
                    }
                }
                finally
                {
                    _aiStatesLock.ExitWriteLock();
                }
            }

            TargetAiStateCount = count;
        }
        finally
        {
            _aiStatesLock.ExitUpgradeableReadLock();
        }
    }

    private void AiReset()
    {
        _aiStatesLock.EnterWriteLock();
        try
        {
            _aiStates.Clear();
            _aiStates.Add(new AiState(this));
        }
        finally
        {
            _aiStatesLock.ExitWriteLock();
        }
    }
}