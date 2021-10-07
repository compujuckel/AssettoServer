using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using AssettoServer.Network.Packets.Outgoing;
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
                else if(entryCar.Client != null && entryCar.Client.HasSentFirstUpdate)
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

            if (spawnPoint != null)
            {
                var lanes = spawnPoint.GetLanes();
                spawnPoint = lanes[_random.Next(0, lanes.Count)]; //GetRandomWeighted(lanes.Count)];
            }

            while (spawnPoint != null && !IsPositionSafe(spawnPoint.Point))
            {
                spawnPoint = spawnPoint.Traverse(1);
                var lanes = spawnPoint.GetLanes();
                spawnPoint = lanes[_random.Next(0, lanes.Count)]; //GetRandomWeighted(lanes.Count)];
            }

            return spawnPoint;
        }

        public void ObstacleDetection()
        {
            var aiStates = _server.EntryCars.Where(car => car.AiControlled).SelectMany(car => car.AiStates);

            foreach (var aiState in aiStates)
            {
                aiState.DetectObstacles();
            }
        }

        public void AdjustOverbooking()
        {
            int playerCount = _server.EntryCars.Count(car => car.Client != null && car.Client.IsConnected);
            int aiCount = _server.EntryCars.Count(car => car.AiControlled);

            int targetAiCount = Math.Min(playerCount * Math.Min(_server.Configuration.Extra.AiParams.AiPerPlayerTargetCount, aiCount), _server.Configuration.Extra.AiParams.MaxAiTargetCount);

            int overbooking = (int) Math.Max(1, Math.Ceiling((float) targetAiCount / aiCount));
            _server.ChatLog.Debug("Overbooking update, #Players {0} #AIs {1} #Target {2} -> {3}", playerCount, aiCount, targetAiCount, overbooking);
            
            SetAiOverbooking(overbooking);
        }
        
        public void SetAiOverbooking(int count)
        {
            var aiCars = _server.EntryCars.Where(car => car.AiControlled && car.Client == null);

            foreach (var aiCar in aiCars)
            {
                aiCar.SetAiOverbooking(count);
            }
        }

        private bool CanSpawnState(Vector3 spawnPoint, AiState aiState)
        {
            // Remove state if AI slot overbooking was reduced
            if (aiState.EntryCar.AiStates.IndexOf(aiState) >= aiState.EntryCar.TargetAiStateCount)
            {
                aiState.EntryCar.AiStates.Remove(aiState);
                _server.ChatLog.Debug("Removed state of Traffic {0} due to overbooking reduction", aiState.EntryCar.SessionId);

                if (aiState.EntryCar.AiStates.Count == 0)
                {
                    Log.Debug("Traffic {0} has no states left, disconnecting", aiState.EntryCar.SessionId);
                    _server.BroadcastPacket(new CarDisconnected { SessionId = aiState.EntryCar.SessionId });
                }
                return false;
            }
            
            foreach (var state in aiState.EntryCar.AiStates)
            {
                if (state == aiState || !state.Initialized) continue;

                if (Vector3.DistanceSquared(spawnPoint, state.Status.Position) < _server.Configuration.Extra.AiParams.StateSafetyDistanceSquared)
                {
                    return false;
                }
            }
            return true;
        }

        private void InitializeState(AiState state)
        {
            int spawnSpline = _random.Next(0, _server.TrafficMap.Splines.Count - 1);
            int spawnPos = _random.Next(0, _server.TrafficMap.Splines[spawnSpline].Points.Length);
            var spawnPoint = _server.TrafficMap.Splines[spawnSpline].Points[spawnPos];
            
            while (spawnPoint != null && !IsPositionSafe(spawnPoint.Point) && !CanSpawnState(spawnPoint.Point, state))
            {
                spawnPoint = spawnPoint.Traverse(5);
            }

            if (spawnPoint != null)
            {
                state.Teleport(_server.TrafficMap.Splines[spawnSpline].Points[spawnPos], true);
            }
        }
        
        public void Update()
        {
            var playerCars = _server.EntryCars.Where(car => !car.AiControlled
                                                            && car.Client != null
                                                            && car.Client.HasSentFirstUpdate
                                                            && Environment.TickCount64 - car.LastActiveTime < _server.Configuration.Extra.AiParams.PlayerAfkTimeoutMilliseconds
            ).ToList();
            var initializedAiStates = _server.EntryCars.Where(car => car.AiControlled).SelectMany(car => car.AiStates).Where(state => state.Initialized);
            var distances = new List<AiDistance>();

            foreach(var aiState in initializedAiStates)
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

            var uninitializedAiStates = _server.EntryCars.Where(car => car.AiControlled).SelectMany(car => car.AiStates).Where(state => !state.Initialized);
            foreach (var state in uninitializedAiStates)
            {
                InitializeState(state);
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

            while (playerCarsOrdered.Count > 0 && outOfRangeAiStates.Count > 0)
            {
                TrafficSplinePoint spawnPoint = null;
                while (spawnPoint == null && playerCarsOrdered.Count > 0)
                {
                    var targetPlayerCar = playerCarsOrdered.ElementAt(GetRandomWeighted(playerCarsOrdered.Count));
                    playerCarsOrdered.Remove(targetPlayerCar);

                    spawnPoint = GetSpawnPoint(targetPlayerCar);
                }

                if (spawnPoint == null)
                    continue;

                foreach (var targetAiState in outOfRangeAiStates)
                {
                    if (!CanSpawnState(spawnPoint.Point, targetAiState))
                        continue;

                    targetAiState.SpawnProtectionEnds = Environment.TickCount64 + _random.Next(_server.Configuration.Extra.AiParams.MinSpawnProtectionTimeMilliseconds, _server.Configuration.Extra.AiParams.MaxSpawnProtectionTimeMilliseconds);
                    targetAiState.SafetyDistanceSquared = _random.Next(_server.Configuration.Extra.AiParams.MinAiSafetyDistanceSquared, _server.Configuration.Extra.AiParams.MaxAiSafetyDistanceSquared);
                    targetAiState.Teleport(spawnPoint, true);

                    outOfRangeAiStates.Remove(targetAiState);
                    break;
                }
            }
        }
    }
}