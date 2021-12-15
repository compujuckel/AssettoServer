using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
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
    public byte[] LastSeenAiSpawn { get; init; }
    public byte[] AiPakSequenceIds { get; init; }
    public AiState[] LastSeenAiState { get; init; }
    public ImmutableList<AiState> AiStates { get; private set; }
    public string AiName { get; private set; }

    public void AiInit(int maxAiStates)
    {
        AiName = $"{Server.Configuration.Extra.AiParams.NamePrefix} {SessionId}";
        
        var builder = ImmutableList.CreateBuilder<AiState>();
        for (var i = 0; i < maxAiStates; i++)
        {
            builder.Add(new AiState(this));
        }

        AiStates = builder.ToImmutable();
    }

    public void RemoveUnsafeStates()
    {
        foreach (var aiState in AiStates)
        {
            if (!aiState.Active || !aiState.Initialized) continue;

            foreach (var targetAiState in AiStates)
            {
                if (aiState != targetAiState
                    && targetAiState.Active
                    && targetAiState.Initialized
                    && Vector3.DistanceSquared(aiState.Status.Position, targetAiState.Status.Position) < Server.Configuration.Extra.AiParams.MinStateDistanceSquared
                    && Vector3.Dot(aiState.Status.Velocity, targetAiState.Status.Velocity) > 0)
                {
                    aiState.ForceRespawn();
                    Log.Debug("Removed close state from AI {0}", SessionId);
                }
            }
        }
    }

    public AiState GetBestStateForPlayer(CarStatus playerStatus)
    {
        AiState bestState = null;
        float minDistance = float.MaxValue;

        foreach (var aiState in AiStates)
        {
            if (!aiState.Active || !aiState.Initialized) continue;

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

    public bool IsPositionSafe(Vector3 position)
    {
        foreach (var aiState in AiStates)
        {
            if (aiState.Initialized && Vector3.DistanceSquared(aiState.Status.Position, position) < aiState.SafetyDistanceSquared)
            {
                return false;
            }
        }

        return true;
    }

    public (AiState aiState, float distanceSquared) GetClosestAiState(Vector3 position)
    {
        AiState closestState = null;
        float minDistanceSquared = float.MaxValue;

        foreach (var aiState in AiStates)
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

    public bool CanSpawnAiState(Vector3 spawnPoint, AiState aiState)
    {

        // Remove state if AI slot overbooking was reduced
        if (AiStates.IndexOf(aiState) >= TargetAiStateCount)
        {
            aiState.SetActive(false);

            Log.Verbose("Removed state of Traffic {0} due to overbooking reduction", SessionId);

            if (!AiStates.Any(s => s.Active))
            {
                Log.Verbose("Traffic {0} has no states left, disconnecting", SessionId);
                Server.BroadcastPacket(new CarDisconnected { SessionId = SessionId });
            }

            return false;
        }

        foreach (var state in AiStates)
        {
            if (state == aiState || !state.Active || !state.Initialized) continue;

            if (Vector3.DistanceSquared(spawnPoint, state.Status.Position) < Server.Configuration.Extra.AiParams.StateSpawnDistanceSquared)
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
                Log.Debug("Slot {0} is now controlled by AI", SessionId);

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
                Log.Debug("Slot {0} is no longer controlled by AI", SessionId);
                if (AiStates.Any(s => s.Active))
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
        int activeCount = AiStates.Count(s => s.Active);
        
        if (count > activeCount)
        {
            for (int i = activeCount; i < count; i++)
            {
                AiStates[i].SetActive(true);
            }
        }

        TargetAiStateCount = count;
    }

    private void AiReset()
    {
        foreach (var aiState in AiStates)
        {
            aiState.SetActive(false);
        }
    }
}