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

        private const float PlayerRadius = 300.0f;
        private const long PlayerAfkTimeout = 30_000;
        private const int MinimumSpawnDistance = 100;
        private const int MaximumSpawnDistance = 400;
        private const int MinimumAiSafetyDistanceSquared = 15 * 15;
        private const int MaximumAiSafetyDistanceSquared = 40 * 40;
        private const float SpawnSafetyDistanceToPlayerSquared = 50 * 50;
        private const int MinimumSpawnProtectionTime = 5_000;
        private const int MaximumSpawnProtectionTime = 10_000;

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
            using var _ = Operation.Time("AI update");

            List<EntryCar> playerCars = _server.EntryCars.Where(car => !car.AiControlled 
                                                                       && car.Client != null 
                                                                       && car.Client.HasSentFirstUpdate 
                                                                       && Environment.TickCount64 - car.LastActiveTime < PlayerAfkTimeout
                                                                       ).ToList();
            List<EntryCar> aiCars = _server.EntryCars.Where(car => car.AiControlled).ToList();
            List<AiDistance> distances = new List<AiDistance>();

            foreach(var aiCar in aiCars)
            {
                foreach (var playerCar in playerCars)
                {
                    distances.Add(new AiDistance()
                    {
                        AiCar = aiCar,
                        PlayerCar = playerCar,
                        Distance = Vector3.Distance(aiCar.Status.Position, playerCar.Status.Position)
                    });
                }
            }

            // Find all AIs that are at least x meters away from all players
            var outOfRangeAiCars = from distance in distances
                group distance by distance.AiCar
                into aiGroup
                where Environment.TickCount64 > aiGroup.Key.AiSpawnProtectionEnds && aiGroup.Min(d => d.Distance) > PlayerRadius
                orderby aiGroup.Min(d => d.Distance) descending 
                select aiGroup.Key;

            var playerCarsOrdered = from distance in distances
                group distance by distance.PlayerCar
                into playerGroup
                orderby playerGroup.Min(d => d.Distance) descending 
                select playerGroup.Key;
            
            int maxRand = GetTriangleNumber(playerCars.Count);
            int rand = _random.Next(maxRand);
            int target = 0;
            for (int i = playerCars.Count; i < maxRand; i += (i - 1))
            {
                if (rand < i) break;
                target++;
            }

            var targetAiCar = outOfRangeAiCars.First();
            var targetPlayerCar = playerCarsOrdered.ElementAt(target);
            
            int splinePos = _server.AiSpline.WorldToSpline(targetPlayerCar.Status.Position);
            int spawnPoint = splinePos + _random.Next(MinimumSpawnDistance, MaximumSpawnDistance);;
            while (!IsPositionSafe(_server.AiSpline.SplineToWorld(spawnPoint)))
            {
                spawnPoint += 10;
            }
            
            int spawnProtection = _random.Next(MinimumSpawnProtectionTime, MaximumSpawnProtectionTime);

            Log.Debug("Moving AI {0} to {1}, spline {2} -> spawn {3} prob {4}/{5}",targetAiCar.SessionId, targetPlayerCar.Client.Name, splinePos, spawnPoint, rand, maxRand);

            targetAiCar.AiSpawnProtectionEnds = Environment.TickCount64 + spawnProtection;
            targetAiCar.AiSafetyDistanceSquared = _random.Next(MinimumAiSafetyDistanceSquared, MaximumAiSafetyDistanceSquared);
            targetAiCar.AiMoveToSplinePosition(spawnPoint, true);
        }
    }
}