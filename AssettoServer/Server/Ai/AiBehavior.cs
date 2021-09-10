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
        private const int SpawnDistanceVariation = 300;
        private const float SpawnSafetyDistanceToAiSquared = 25 * 25;
        private const float SpawnSafetyDistanceToPlayerSquared = 50 * 50;
        private const int MinimumSpawnProtection = 5_000;
        private const int SpawnProtectionVariation = 5_000;

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

        private static bool IsPositionSafe(IEnumerable<EntryCar> entryCars, Vector3 position)
        {
            foreach(var entryCar in entryCars)
            {
                if (Vector3.DistanceSquared(entryCar.Status.Position, position) < (entryCar.AiControlled ? SpawnSafetyDistanceToAiSquared : SpawnSafetyDistanceToPlayerSquared))
                    return false;
            }

            return true;
        }
        
        public async Task UpdateAsync()
        {
            using var _ = Operation.Time("AI update");

            /*foreach (var entryCar in _server.EntryCars)
            {
                // Cars with enabled AI and no client should be AI controlled
                if (entryCar.AiMode != AiMode.Disabled && entryCar.Client == null && !entryCar.AiControlled)
                { 
                    entryCar.AiControlled = true;
                    Log.Debug("Slot {0} is now controlled by AI", entryCar.SessionId);
                }
                // Cars with auto AI and a client should not be AI controlled (-> give control to human player)
                if (entryCar.AiMode == AiMode.Auto && entryCar.Client != null && entryCar.AiControlled)
                {
                    entryCar.AiControlled = false;
                    Log.Debug("Slot {0} is no longer controlled by AI", entryCar.SessionId);
                }
            }*/
            
            List<EntryCar> playerCars = _server.EntryCars.Where(car => !car.AiControlled 
                                                                       && car.Client != null 
                                                                       && car.Client.HasSentFirstUpdate 
                                                                       && Environment.TickCount64 - car.LastActiveTime < PlayerAfkTimeout
                                                                       ).ToList();
            List<EntryCar> aiCars = _server.EntryCars.Where(car => car.AiControlled).ToList();
            List<AiDistance> distances = new List<AiDistance>();
            
            //Log.Debug("Players: {0} AIs: {1}", playerCars.Count, aiCars.Count);

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
                select aiGroup.Key;
            
            var playerCarsOrdered = from distance in distances
                group distance by distance.PlayerCar
                into aiGroup
                orderby aiGroup.Min(d => d.Distance) descending 
                select aiGroup.Key;
            
            int maxRand = GetTriangleNumber(playerCars.Count);
            int rand = _random.Next(maxRand);
            int target = 0;
            for (int i = playerCars.Count; i < maxRand; i += (i - 1))
            {
                if (rand < i)
                {
                    break;
                }

                target++;
            }

            var targetAiCar = outOfRangeAiCars.First();
            var targetPlayerCar = playerCarsOrdered.ElementAt(target);
            int splinePos = _server.AiSpline.WorldToSpline(targetPlayerCar.Status.Position);
            int spawnPoint = splinePos + MinimumSpawnDistance + _random.Next(SpawnDistanceVariation);;
            do
            {
                spawnPoint += 10;
            } while (!IsPositionSafe(_server.EntryCars, _server.AiSpline.SplineToWorld(spawnPoint)));
            
            int spawnProtection = MinimumSpawnProtection + _random.Next(SpawnProtectionVariation);
            string msg = $"Moving AI {targetAiCar.SessionId} near {targetPlayerCar.Client.Name}, prob {rand}/{maxRand}";
            _server.BroadcastPacket(new ChatMessage { SessionId = 255, Message = msg });
            Log.Debug("Moving AI {0} near {1}, spline pos {2} -> spawn pos {3} protect {4}ms",targetAiCar.SessionId, targetPlayerCar.Client.Name, splinePos, spawnPoint, spawnProtection);

            targetAiCar.AiSpawnProtectionEnds = Environment.TickCount64 + spawnProtection;
            targetAiCar.AiMoveToSplinePosition(spawnPoint, true);
        }
    }
}