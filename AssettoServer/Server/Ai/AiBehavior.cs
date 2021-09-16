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

        private const float PlayerRadius = 200.0f;
        private const long PlayerAfkTimeout = 10_000;
        private const float PlayerPositionOffset = 100.0f;
        private const float MaxPlayerDistanceToAiSplineSquared = 7 * 7;
        private const int MinSpawnDistance = 100;
        private const int MaxSpawnDistance = 400;
        private const int MinAiSafetyDistanceSquared = 20 * 20;
        private const int MaxAiSafetyDistanceSquared = 70 * 70;
        private const float SpawnSafetyDistanceToPlayerSquared = 80 * 80;
        private const int MinSpawnProtectionTime = 4_000;
        private const int MaxSpawnProtectionTime = 8_000;

        public AiBehavior(ACServer server)
        {
            _server = server;
        }

        private struct AiDistance
        {
            public EntryCar AiCar;
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
                if (Vector3.DistanceSquared(entryCar.Status.Position, position) < (entryCar.AiControlled ? entryCar.AiSafetyDistanceSquared : SpawnSafetyDistanceToPlayerSquared))
                    return false;
            }

            return true;
        }

        public async Task UpdateAsync()
        {
            //using var _ = Operation.Time("AI update");

            var playerCars = _server.EntryCars.Where(car => !car.AiControlled 
                                                            && car.Client != null 
                                                            && car.Client.HasSentFirstUpdate 
                                                            && Environment.TickCount64 - car.LastActiveTime < PlayerAfkTimeout
                                                            && car.Status.Velocity.LengthSquared() > 1
                                                                       ).ToList();
            var aiCars = _server.EntryCars.Where(car => car.AiControlled);
            var distances = new List<AiDistance>();

            foreach(var aiCar in aiCars)
            {
                foreach (var playerCar in playerCars)
                {
                    var offsetPosition = playerCar.Status.Position + Vector3.Normalize(playerCar.Status.Velocity) * PlayerPositionOffset;
                    
                    distances.Add(new AiDistance()
                    {
                        AiCar = aiCar,
                        PlayerCar = playerCar,
                        Distance = Vector3.Distance(aiCar.Status.Position, offsetPosition)
                    });
                }
            }

            // Find all AIs that are at least <PlayerRadius> meters away from all players. Highest distance AI will be teleported
            var outOfRangeAiCars = from distance in distances
                group distance by distance.AiCar
                into aiGroup
                where Environment.TickCount64 > aiGroup.Key.AiSpawnProtectionEnds && aiGroup.Min(d => d.Distance) > PlayerRadius
                orderby aiGroup.Min(d => d.Distance) descending 
                select aiGroup.Key;

            // Order player cars by their minimum distance to an AI. Higher distance -> higher probability for the next AI to spawn next to them
            var playerCarsOrdered = (from distance in distances
                group distance by distance.PlayerCar
                into playerGroup
                orderby playerGroup.Min(d => d.Distance) descending
                select playerGroup.Key).ToList();

            var targetAiCar = outOfRangeAiCars.FirstOrDefault();
            
            if (playerCarsOrdered.Count == 0 || targetAiCar == null)
                return;

            var targetPlayerCar = playerCarsOrdered.ElementAt(GetRandomWeighted(playerCarsOrdered.Count));;
            var targetPlayerSplinePos = _server.AiSpline.WorldToSpline(targetPlayerCar.Status.Position);
            
            // Do not not spawn if a player is too far away from the AI spline, e.g. in pits or in a part of the map without traffic
            while (targetPlayerSplinePos.distanceSquared > MaxPlayerDistanceToAiSplineSquared)
            {
                playerCarsOrdered.Remove(targetPlayerCar);
                if (playerCarsOrdered.Count == 0)
                    break;
                
                targetPlayerCar = playerCarsOrdered.ElementAt(GetRandomWeighted(playerCarsOrdered.Count));
                targetPlayerSplinePos = _server.AiSpline.WorldToSpline(targetPlayerCar.Status.Position);
            }

            int spawnPoint = (targetPlayerSplinePos.position + _random.Next(MinSpawnDistance, MaxSpawnDistance)) % _server.AiSpline.IdealLine.Length;
            while (!IsPositionSafe(_server.AiSpline.SplineToWorld(spawnPoint)))
            {
                spawnPoint += 10;
                spawnPoint %= _server.AiSpline.IdealLine.Length;
            }

            targetAiCar.AiSpawnProtectionEnds = Environment.TickCount64 + _random.Next(MinSpawnProtectionTime, MaxSpawnProtectionTime);
            targetAiCar.AiSafetyDistanceSquared = _random.Next(MinAiSafetyDistanceSquared, MaxAiSafetyDistanceSquared);
            targetAiCar.AiMoveToSplinePosition(spawnPoint, 0, true);
        }
    }
}