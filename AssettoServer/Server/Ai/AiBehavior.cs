using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using AssettoServer.Network.Packets.Shared;
using Serilog;
using SerilogTimings;

namespace AssettoServer.Server.Ai
{
    public class AiBehavior
    {
        private readonly ACServer _server;
        private readonly Random _random = new();

        public AiBehavior(ACServer server)
        {
            _server = server;
        }

        private struct AiDistance
        {
            public AiState AiCar;
            public EntryCar PlayerCar;
            public float Distance;
        }

        private static int GetTriangleNumber(int n)
        {
            return (n * (n + 1)) / 2;
        }

        private int GetRandomWeighted(int max)
        {
            // Probabilities for max = 4
            // 0    4/10
            // 1    3/10
            // 2    2/10
            // 3    1/10
            
            int maxRand = GetTriangleNumber(max);
            int rand = _random.Next(maxRand);
            int target = 0;
            for (int i = max; i < maxRand; i += (i - 1))
            {
                if (rand < i) break;
                target++;
            }

            return target;
        }

        private bool IsPositionSafe(Vector3 position)
        {
            foreach(var entryCar in _server.EntryCars)
            {
                if (entryCar.AiControlled)
                {
                    if (entryCar.AiStates.Any(state => Vector3.DistanceSquared(state.Status.Position, position) < state.SafetyDistanceSquared))
                    {
                        return false;
                    }
                }
                else
                {
                    if (Vector3.DistanceSquared(entryCar.Status.Position, position) < _server.Configuration.Extra.AiParams.SpawnSafetyDistanceToPlayerSquared)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private TrafficSplinePoint GetSpawnPoint(EntryCar playerCar)
        {
            var targetPlayerSplinePos = _server.TrafficMap.WorldToSpline(playerCar.Status.Position);
            
            // Do not not spawn if a player is too far away from the AI spline, e.g. in pits or in a part of the map without traffic
            if (targetPlayerSplinePos.distanceSquared > _server.Configuration.Extra.AiParams.MaxPlayerDistanceToAiSplineSquared)
            {
                return null;
            }
            
            int spawnDistance = _random.Next(_server.Configuration.Extra.AiParams.MinSpawnDistance, _server.Configuration.Extra.AiParams.MaxSpawnDistance);
            var spawnPoint = targetPlayerSplinePos.point.Traverse(spawnDistance);

            if (spawnPoint == null)
            {
                return null;
            }

            while (!IsPositionSafe(spawnPoint.Point))
            {
                spawnPoint = spawnPoint.Traverse(1);

                if (spawnPoint == null)
                {
                    return null;
                }
            }

            return spawnPoint;
        }

        public async Task ObstacleDetectionAsync()
        {
            //using var _ = Operation.Time("AI obstacle detections");
            
            var aiCars = _server.EntryCars.Where(car => car.AiControlled).ToList();

            foreach (var aiCar in aiCars)
            {
                foreach (var aiState in aiCar.AiStates)
                {
                    aiState.DetectObstacles();
                }
            }
        }
        
        public void SetAiOverbooking(int count)
        {
            var aiCars = _server.EntryCars.Where(car => car.AiControlled).ToList();

            foreach (var aiCar in aiCars)
            {
                aiCar.SetAiOverbooking(count);
            }
        }

        private bool CanSpawnState(Vector3 spawnPoint, AiState aiState)
        {
            int index = aiState.EntryCar.AiStates.IndexOf(aiState);
            if (index >= aiState.EntryCar.TargetAiStateCount)
            {
                aiState.EntryCar.AiStates.Remove(aiState);
                Log.Debug("removed idx {0}, target {1}, max {2}", index, aiState.EntryCar.TargetAiStateCount, _server.Configuration.Extra.AiParams.MaxOverbooking);
                return false;
            }
            
            foreach (var state in aiState.EntryCar.AiStates)
            {
                if (state == aiState) continue;

                if (Vector3.DistanceSquared(spawnPoint, state.Status.Position) < _server.Configuration.Extra.AiParams.StateSafetyDistanceSquared)
                {
                    return false;
                }
            }
            return true;
        }

        public async Task UpdateAsync()
        {
            //using var _ = Operation.Time("AI update");

            var playerCars = _server.EntryCars.Where(car => !car.AiControlled
                                                            && car.Client != null
                                                            && car.Client.HasSentFirstUpdate
                                                            && Environment.TickCount64 - car.LastActiveTime < _server.Configuration.Extra.AiParams.PlayerAfkTimeoutMilliseconds
                //&& car.Status.Velocity.LengthSquared() > 1
            ).ToList();
            var aiStates = _server.EntryCars.Where(car => car.AiControlled).SelectMany(car => car.AiStates);
            var distances = new List<AiDistance>();

            foreach(var aiState in aiStates)
            {
                foreach (var playerCar in playerCars)
                {
                    var offsetPosition = playerCar.Status.Position + Vector3.Normalize(playerCar.Status.Velocity) * _server.Configuration.Extra.AiParams.PlayerPositionOffset;
                    
                    distances.Add(new AiDistance()
                    {
                        AiCar = aiState,
                        PlayerCar = playerCar,
                        Distance = Vector3.Distance(aiState.Status.Position, offsetPosition)
                    });
                }
            }

            // Find all AIs that are at least <PlayerRadius> meters away from all players. Highest distance AI will be teleported
            var outOfRangeAiStates = (from distance in distances
                group distance by distance.AiCar
                into aiGroup
                where Environment.TickCount64 > aiGroup.Key.SpawnProtectionEnds && aiGroup.Min(d => d.Distance) > _server.Configuration.Extra.AiParams.PlayerRadius
                orderby aiGroup.Min(d => d.Distance) descending 
                select aiGroup.Key).ToList();

            // Order player cars by their minimum distance to an AI. Higher distance -> higher probability for the next AI to spawn next to them
            var playerCarsOrdered = (from distance in distances
                group distance by distance.PlayerCar
                into playerGroup
                orderby playerGroup.Min(d => d.Distance) descending
                select playerGroup.Key).ToList();

            if (playerCarsOrdered.Count == 0 || outOfRangeAiStates.Count == 0)
                return;


            TrafficSplinePoint spawnPoint;
            EntryCar targetPlayerCar;
            do
            {
                if (playerCarsOrdered.Count == 0)
                {
                    //Log.Warning("No player available for spawning");
                    return;
                }
                
                targetPlayerCar = playerCarsOrdered.ElementAt(GetRandomWeighted(playerCarsOrdered.Count));
                playerCarsOrdered.Remove(targetPlayerCar);
                
                spawnPoint = GetSpawnPoint(targetPlayerCar);
            } while (spawnPoint == null);

            foreach (var targetAiState in outOfRangeAiStates)
            {
                if (!CanSpawnState(spawnPoint.Point, targetAiState))
                {
                    //Log.Warning("Target player {0} is too close to another state of AI {1}", targetPlayerCar.Client.Name, targetAiState.EntryCar.SessionId);
                    continue;
                }

                targetAiState.SpawnProtectionEnds = Environment.TickCount64 + _random.Next(_server.Configuration.Extra.AiParams.MinSpawnProtectionTimeMilliseconds, _server.Configuration.Extra.AiParams.MaxSpawnProtectionTimeMilliseconds);
                targetAiState.SafetyDistanceSquared = _random.Next(_server.Configuration.Extra.AiParams.MinAiSafetyDistanceSquared, _server.Configuration.Extra.AiParams.MaxAiSafetyDistanceSquared);
                targetAiState.Teleport(spawnPoint);
                break;
            }
        }
    }
}