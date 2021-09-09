using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Serilog;
using SerilogTimings;

namespace AssettoServer.Server.Ai
{
    public class AiBehavior
    {
        private readonly ACServer _server;
        private readonly Random _random = new();

        private const float PlayerRadius = 300.0f;
        private const int MinimumSpawnDistance = 100;
        private const int SpawnDistanceVariation = 300;
        private const int SpawnSafetyDistanceSquared = 20 * 20;
        private const int MinimumSpawnProtection = 5000;
        private const int SpawnProtectionVariation = 5000;

        public AiBehavior(ACServer server)
        {
            _server = server;
        }

        private struct AiDistance
        {
            public AiCar AiCar;
            public EntryCar PlayerCar;
            public float Distance;
        }

        private static bool IsPositionSafe(IEnumerable<EntryCar> entryCars, Vector3 position)
        {
            foreach(var entryCar in entryCars)
            {
                if (Vector3.DistanceSquared(entryCar.Status.Position, position) < SpawnSafetyDistanceSquared)
                    return false;
            }

            return true;
        }
        
        public async Task UpdateAsync()
        {
            using var _ = Operation.Time("AI update");
            
            IEnumerable<EntryCar> playerCars = _server.EntryCars.Where(car => car is not AiCar && car.Client != null && car.Client.IsConnected).ToList();
            IEnumerable<AiCar> aiCars = _server.EntryCars.OfType<AiCar>().ToList();
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
                where Environment.TickCount64 > aiGroup.Key.SpawnProtectionEnds && aiGroup.Min(d => d.Distance) > PlayerRadius
                select aiGroup.Key;
            
            var playerCarsWithoutCloseAi = from distance in distances
                group distance by distance.PlayerCar
                into aiGroup
                orderby aiGroup.Min(d => d.Distance) descending 
                select aiGroup.Key;

            foreach (var aiCar in outOfRangeAiCars)
            {
                var playerCar = playerCarsWithoutCloseAi.First();
                int splinePos = _server.AiSpline.WorldToSpline(playerCar.Status.Position);
                int spawnPoint = splinePos + MinimumSpawnDistance + _random.Next(SpawnDistanceVariation);;
                do
                {
                    spawnPoint += 10;
                } while (!IsPositionSafe(_server.EntryCars, _server.AiSpline.SplineToWorld(spawnPoint)));
                
                int spawnProtection = MinimumSpawnProtection + _random.Next(SpawnProtectionVariation);
                Log.Debug("Moving AI {0} near {1}, spline pos {2} -> spawn pos {3} protect {4}ms",aiCar.SessionId, playerCar.Client.Name, splinePos, spawnPoint, spawnProtection);

                aiCar.SpawnProtectionEnds = Environment.TickCount64 + spawnProtection;
                aiCar.MoveToSplinePosition(spawnPoint, true);
                return;
            }
        }
    }
}