using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using App.Metrics;
using App.Metrics.Gauge;
using App.Metrics.Timer;
using AssettoServer.Network.Tcp;
using Serilog;

namespace AssettoServer.Server.Ai
{
    public class AiBehavior
    {
        private readonly ACServer _server;
        private long _lastAiUpdate = Environment.TickCount64;
        private long _lastAiObstacleDetectionUpdate = Environment.TickCount64;
        
        public ILookup<TrafficSplinePoint, AiState> AiStatesBySplinePoint { get; private set; }

        private readonly GaugeOptions _aiStateCountMetric = new GaugeOptions
        {
            Name = "AiStateCount",
            MeasurementUnit = Unit.Items
        };
        private readonly TimerOptions _updateDurationMetric = new TimerOptions
        {
            Name = "AiBehavior.Update",
            MeasurementUnit = Unit.Calls,
            DurationUnit = TimeUnit.Milliseconds,
            RateUnit = TimeUnit.Milliseconds
        };
        private readonly TimerOptions _obstacleDetectionDurationMetric = new TimerOptions
        {
            Name = "AiBehavior.ObstacleDetection",
            MeasurementUnit = Unit.Calls,
            DurationUnit = TimeUnit.Milliseconds,
            RateUnit = TimeUnit.Milliseconds
        };

        public AiBehavior(ACServer server)
        {
            _server = server;

            _server.ClientChecksumPassed += OnClientChecksumPassed;
            _server.ClientDisconnected += OnClientDisconnected;
            _server.Update += OnUpdate;
        }

        private void OnUpdate(object sender, EventArgs args)
        {
            foreach (var entryCar in _server.EntryCars)
            {
                if (entryCar.AiControlled)
                {
                    entryCar.AiUpdate();
                }
            }
            
            if (Environment.TickCount64 - _lastAiUpdate > 500)
            {
                _lastAiUpdate = Environment.TickCount64;
                Task.Run(Update)
                    .ContinueWith(t => Log.Error(t.Exception, "Error in AI update"), TaskContinuationOptions.OnlyOnFaulted);
            }

            if (Environment.TickCount64 - _lastAiObstacleDetectionUpdate > 100)
            {
                _lastAiObstacleDetectionUpdate = Environment.TickCount64;
                Task.Run(ObstacleDetection)
                    .ContinueWith(t => Log.Error(t.Exception, "Error in AI obstacle detection"), TaskContinuationOptions.OnlyOnFaulted);
            }
        }

        private void OnClientChecksumPassed(ACTcpClient sender, EventArgs args)
        {
            sender.EntryCar.SetAiControl(false);
            AdjustOverbooking();
        }

        private void OnClientDisconnected(ACTcpClient sender, EventArgs args)
        {
            if (sender.EntryCar.AiMode == AiMode.Auto)
            {
                sender.EntryCar.SetAiControl(true);
                AdjustOverbooking();
            }
        }

        private struct AiDistance
        {
            public AiState AiCar;
            public EntryCar PlayerCar;
            public float DistanceSquared;
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
            int rand = Random.Shared.Next(maxRand);
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
                if (entryCar.AiControlled && !entryCar.IsPositionSafe(position))
                {
                    return false;
                }
                
                if (entryCar.Client != null 
                    && entryCar.Client.HasSentFirstUpdate
                    && Vector3.DistanceSquared(entryCar.Status.Position, position) < _server.Configuration.Extra.AiParams.SpawnSafetyDistanceToPlayerSquared)
                {
                    return false;
                }
            }

            return true;
        }

        private TrafficSplinePoint GetSpawnPoint(EntryCar playerCar)
        {
            var targetPlayerSplinePos = _server.TrafficMap.WorldToSpline(playerCar.Status.Position);

            var forward = targetPlayerSplinePos.point.Next.Point - targetPlayerSplinePos.point.Point;
            int direction = Vector3.Dot(forward, playerCar.Status.Velocity) > 0 ? 1 : -1;
            
            // Do not not spawn if a player is too far away from the AI spline, e.g. in pits or in a part of the map without traffic
            if (targetPlayerSplinePos.distanceSquared > _server.Configuration.Extra.AiParams.MaxPlayerDistanceToAiSplineSquared)
            {
                return null;
            }
            
            int spawnDistance = Random.Shared.Next(_server.Configuration.Extra.AiParams.MinSpawnDistancePoints, _server.Configuration.Extra.AiParams.MaxSpawnDistancePoints);
            var spawnPoint = targetPlayerSplinePos.point.Traverse(spawnDistance * direction)?.RandomLane(_server.Configuration.Extra.AiParams.TwoWayTraffic);
            
            if (spawnPoint != null)
            {
                forward = spawnPoint.Next.Point - spawnPoint.Point;
                direction = Vector3.Dot(forward, playerCar.Status.Velocity) > 0 ? 1 : -1;
            }

            while (spawnPoint != null && !IsPositionSafe(spawnPoint.Point))
            {
                spawnPoint = spawnPoint.Traverse(direction);
            }

            return spawnPoint;
        }

        public void ObstacleDetection()
        {
            using var timer = _server.Metrics.Measure.Timer.Time(_obstacleDetectionDurationMetric);
            
            AiStatesBySplinePoint = _server.EntryCars
                .Where(car => car.AiControlled)
                .SelectMany(car => car.GetAiStatesCopy())
                .Where(state => state.Initialized)
                .ToLookup(state => state.CurrentSplinePoint, state => state);
            
            foreach (var entryCar in _server.EntryCars)
            {
                if (entryCar.AiControlled)
                {
                    entryCar.AiObstacleDetection();
                }
            }
        }

        public void AdjustOverbooking()
        {
            int playerCount = _server.EntryCars.Count(car => car.Client != null && car.Client.IsConnected);
            int aiCount = _server.EntryCars.Count(car => car.Client == null && car.AiControlled); // client null check is necessary here so that slots where someone is connecting don't count

            int targetAiCount = Math.Min(playerCount * Math.Min(_server.Configuration.Extra.AiParams.AiPerPlayerTargetCount, aiCount), _server.Configuration.Extra.AiParams.MaxAiTargetCount);

            int overbooking = (int) Math.Ceiling((float) targetAiCount / aiCount);
            Log.Debug("Overbooking update, #Players {0} #AIs {1} #Target {2} -> {3}", playerCount, aiCount, targetAiCount, overbooking);
            
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
        
        public void Update()
        {
            using var timer = _server.Metrics.Measure.Timer.Time(_updateDurationMetric);
            
            foreach (var entryCar in _server.EntryCars)
            {
                if (entryCar.AiControlled)
                {
                    entryCar.RemoveUnsafeStates();
                }
            }
            
            _server.Metrics.Measure.Gauge.SetValue(_aiStateCountMetric, _server.EntryCars.Sum(entryCar => entryCar.GetActiveAiStateCount()));

            var playerCars = _server.EntryCars.Where(car => !car.AiControlled
                                                            && car.Client != null
                                                            && car.Client.HasSentFirstUpdate
                                                            && Environment.TickCount64 - car.LastActiveTime < _server.Configuration.Extra.AiParams.PlayerAfkTimeoutMilliseconds
            ).ToList();

            var allStates = _server.EntryCars.Where(car => car.AiControlled).SelectMany(car => car.GetAiStatesCopy()).ToList();
            var initializedAiStates = allStates.Where(state => state.Initialized);
            var distances = new List<AiDistance>();

            foreach(var aiState in initializedAiStates)
            {
                foreach (var playerCar in playerCars)
                {
                    var offsetPosition = playerCar.Status.Position + Vector3.Normalize(playerCar.Status.Velocity) * _server.Configuration.Extra.AiParams.PlayerPositionOffsetMeters;
                    
                    distances.Add(new AiDistance()
                    {
                        AiCar = aiState,
                        PlayerCar = playerCar,
                        DistanceSquared = Vector3.DistanceSquared(aiState.Status.Position, offsetPosition)
                    });
                }
            }

            var uninitializedAiStates = allStates.Where(state => !state.Initialized).ToList();

            // Find all AIs that are at least <PlayerRadius> meters away from all players. Highest distance AI will be teleported
            var outOfRangeAiStates = uninitializedAiStates.Concat(from distance in distances
                group distance by distance.AiCar
                into aiGroup
                where Environment.TickCount64 > aiGroup.Key.SpawnProtectionEnds && aiGroup.Min(d => d.DistanceSquared) > _server.Configuration.Extra.AiParams.PlayerRadiusSquared
                orderby aiGroup.Min(d => d.DistanceSquared) descending 
                select aiGroup.Key).ToList();
            
            List<EntryCar> playerCarsOrdered;
            
            // Special case after server startup when no AI cars are spawned yet
            if (distances.Count == 0)
            {
                playerCarsOrdered = playerCars;
            }
            // Order player cars by their minimum distance to an AI. Higher distance -> higher probability for the next AI to spawn next to them
            else
            {
                playerCarsOrdered = (from distance in distances
                    group distance by distance.PlayerCar
                    into playerGroup
                    orderby playerGroup.Min(d => d.DistanceSquared) descending
                    select playerGroup.Key).ToList();
            }

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
                    if (!targetAiState.CanSpawn(spawnPoint.Point))
                        continue;
                    
                    targetAiState.Teleport(spawnPoint);

                    outOfRangeAiStates.Remove(targetAiState);
                    break;
                }
            }
        }
    }
}