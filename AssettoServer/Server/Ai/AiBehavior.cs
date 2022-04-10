using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using App.Metrics;
using App.Metrics.Gauge;
using App.Metrics.Timer;
using AssettoServer.Network.Tcp;
using AssettoServer.Server.Configuration;
using Serilog;

namespace AssettoServer.Server.Ai
{
    public class AiBehavior
    {
        private readonly ACServer _server;
        private readonly DynamicTrafficDensity? _dynamicTrafficDensity;
        private long _lastAiUpdate = Environment.TickCount64;
        private long _lastAiObstacleDetectionUpdate = Environment.TickCount64;

        private readonly List<EntryCar> _playerCars = new();
        private readonly List<AiState> _initializedAiStates = new();
        private readonly List<AiState> _uninitializedAiStates = new();
        private readonly List<AiDistance> _distances = new();
        private readonly TrafficMapView _mapView = new(false);

        public Dictionary<TrafficSplinePoint, AiState> AiStatesBySplinePoint { get; } = new();

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

            if (_server.Configuration.Extra.AiParams.HourlyTrafficDensity != null)
            {
                _dynamicTrafficDensity = new DynamicTrafficDensity(server);
            }

            _server.ClientChecksumPassed += OnClientChecksumPassed;
            _server.ClientDisconnected += OnClientDisconnected;
            _server.ClientCollision += OnCollision;
            _server.Update += OnUpdate;
            _server.Configuration.Reload += OnConfigurationReload;
        }

        private void OnCollision(ACTcpClient sender, CollisionEventArgs args)
        {
            if (args.TargetCar?.AiControlled == true)
            {
                var targetAiState = args.TargetCar.GetClosestAiState(sender.EntryCar.Status.Position);
                if (targetAiState.AiState != null && targetAiState.DistanceSquared < 25 * 25)
                {
                    targetAiState.AiState.StopForCollision();
                }
            }
        }

        private void OnConfigurationReload(ACServerConfiguration sender, EventArgs args)
        {
            AdjustOverbooking();
        }

        private void OnUpdate(object sender, EventArgs args)
        {
            for (var i = 0; i < _server.EntryCars.Length; i++)
            {
                var entryCar = _server.EntryCars[i];
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

        private readonly struct AiDistance
        {
            public readonly AiState AiCar;
            public readonly EntryCar PlayerCar;
            public readonly float DistanceSquared;

            public AiDistance(AiState aiCar, EntryCar playerCar, float distanceSquared)
            {
                AiCar = aiCar;
                PlayerCar = playerCar;
                DistanceSquared = distanceSquared;
            }
        }

        private int GetRandomWeighted(int max)
        {
            // Probabilities for max = 4
            // 0    4/10
            // 1    3/10
            // 2    2/10
            // 3    1/10
            
            int maxRand = max * (max + 1) / 2;
            int rand = Random.Shared.Next(maxRand);
            int target = 0;
            for (int i = max; i < maxRand; i += (i - 1))
            {
                if (rand < i) break;
                target++;
            }

            return target;
        }

        private bool IsPositionSafe(TrafficSplinePoint point)
        {
            for (var i = 0; i < _server.EntryCars.Length; i++)
            {
                var entryCar = _server.EntryCars[i];
                if (entryCar.AiControlled && !entryCar.IsPositionSafe(point))
                {
                    return false;
                }

                if (entryCar.Client?.HasSentFirstUpdate == true
                    && Vector3.DistanceSquared(entryCar.Status.Position, point.Position) < _server.Configuration.Extra.AiParams.SpawnSafetyDistanceToPlayerSquared)
                {
                    return false;
                }
            }

            return true;
        }

        private TrafficSplinePoint? GetSpawnPoint(EntryCar playerCar)
        {
            if (!_server.AiEnabled)
                throw new InvalidOperationException("AI disabled");
            
            var targetPlayerSplinePos = _server.TrafficMap.WorldToSpline(playerCar.Status.Position);

            if (targetPlayerSplinePos.point.Next == null) return null;
            
            int direction = Vector3.Dot(targetPlayerSplinePos.point.GetForwardVector(), playerCar.Status.Velocity) > 0 ? 1 : -1;
            
            // Do not not spawn if a player is too far away from the AI spline, e.g. in pits or in a part of the map without traffic
            if (targetPlayerSplinePos.distanceSquared > _server.Configuration.Extra.AiParams.MaxPlayerDistanceToAiSplineSquared)
            {
                return null;
            }
            
            int spawnDistance = Random.Shared.Next(_server.Configuration.Extra.AiParams.MinSpawnDistancePoints, _server.Configuration.Extra.AiParams.MaxSpawnDistancePoints);
            var spawnPoint = _mapView.Traverse(targetPlayerSplinePos.point, spawnDistance * direction)?.RandomLane(_server.Configuration.Extra.AiParams.TwoWayTraffic);
            
            if (spawnPoint != null && spawnPoint.Next != null)
            {
                direction = Vector3.Dot(spawnPoint.GetForwardVector(), playerCar.Status.Velocity) > 0 ? 1 : -1;
            }

            while (spawnPoint != null && !IsPositionSafe(spawnPoint))
            {
                spawnPoint = _mapView.Traverse(spawnPoint, direction * 5);
            }

            return spawnPoint?.RandomLane(_server.Configuration.Extra.AiParams.TwoWayTraffic);
        }

        private void ObstacleDetection()
        {
            using var timer = _server.Metrics.Measure.Timer.Time(_obstacleDetectionDurationMetric);
            AiStatesBySplinePoint.Clear();

            var tmp = new List<AiState>();
            for (int i = 0; i < _server.EntryCars.Length; i++)
            {
                tmp.Clear();
                var car = _server.EntryCars[i];
                if (car.AiControlled)
                {
                    car.GetInitializedStates(tmp);
                    for (int j = 0; j < tmp.Count; j++)
                    {
                        if (!AiStatesBySplinePoint.TryAdd(tmp[j].CurrentSplinePoint, tmp[j]))
                        {
                            var existing = AiStatesBySplinePoint[tmp[j].CurrentSplinePoint];
                            if (tmp[j].CurrentSpeed < existing.CurrentSpeed)
                            {
                                AiStatesBySplinePoint[tmp[j].CurrentSplinePoint] = tmp[j];
                            }
                        }
                    }
                }
            }

            for (int i = 0; i < _server.EntryCars.Length; i++)
            {
                var entryCar = _server.EntryCars[i];
                if (entryCar.AiControlled)
                {
                    entryCar.AiObstacleDetection();
                }
            }
        }

        private void AdjustOverbooking()
        {
            int playerCount = _server.EntryCars.Count(car => car.Client != null && car.Client.IsConnected);
            var aiSlots = _server.EntryCars.Where(car => car.Client == null && car.AiControlled).ToList(); // client null check is necessary here so that slots where someone is connecting don't count

            int targetAiCount = Math.Min(playerCount * Math.Min((int)Math.Round(_server.Configuration.Extra.AiParams.AiPerPlayerTargetCount * _server.Configuration.Extra.AiParams.TrafficDensity), aiSlots.Count), _server.Configuration.Extra.AiParams.MaxAiTargetCount);

            int overbooking = targetAiCount / aiSlots.Count;
            int rest = targetAiCount % aiSlots.Count;
            
            Log.Debug("AI Slot overbooking update - No. players: {NumPlayers} - No. AI Slots: {NumAiSlots} - Target AI count: {TargetAiCount} - Overbooking: {Overbooking} - Rest: {Rest}", 
                playerCount, aiSlots.Count, targetAiCount, overbooking, rest);

            for (int i = 0; i < aiSlots.Count; i++)
            {
                aiSlots[i].SetAiOverbooking(i < rest ? overbooking + 1 : overbooking);
            }
        }

        private void Update()
        {
            using var timer = _server.Metrics.Measure.Timer.Time(_updateDurationMetric);

            _playerCars.Clear();
            _initializedAiStates.Clear();
            _uninitializedAiStates.Clear();
            _distances.Clear();
            for (int i = 0; i < _server.EntryCars.Length; i++)
            {
                var entryCar = _server.EntryCars[i];
                if (!entryCar.AiControlled
                    && entryCar.Client?.HasSentFirstUpdate == true
                    && Environment.TickCount64 - entryCar.LastActiveTime < _server.Configuration.Extra.AiParams.PlayerAfkTimeoutMilliseconds)
                {
                    _playerCars.Add(entryCar);
                }
                else if (entryCar.AiControlled)
                {
                    entryCar.RemoveUnsafeStates();
                    entryCar.GetInitializedStates(_initializedAiStates, _uninitializedAiStates);
                }
            }
            
            _server.Metrics.Measure.Gauge.SetValue(_aiStateCountMetric, _initializedAiStates.Count);

            for (int i = 0; i < _initializedAiStates.Count; i++)
            {
                for (int j = 0; j < _playerCars.Count; j++)
                {
                    var offsetPosition = _playerCars[j].Status.Position + Vector3.Normalize(_playerCars[j].Status.Velocity) * _server.Configuration.Extra.AiParams.PlayerPositionOffsetMeters;
                    _distances.Add(new AiDistance(_initializedAiStates[i], _playerCars[j], Vector3.DistanceSquared(_initializedAiStates[i].Status.Position, offsetPosition)));
                }
            }

            // Find all AIs that are at least <PlayerRadius> meters away from all players. Highest distance AI will be teleported
            _uninitializedAiStates.AddRange(from distance in _distances
                group distance by distance.AiCar
                into aiGroup
                where Environment.TickCount64 > aiGroup.Key.SpawnProtectionEnds && aiGroup.Min(d => d.DistanceSquared) > _server.Configuration.Extra.AiParams.PlayerRadiusSquared
                orderby aiGroup.Min(d => d.DistanceSquared) descending
                select aiGroup.Key);
            
            // Order player cars by their minimum distance to an AI. Higher distance -> higher probability for the next AI to spawn next to them
            if(_distances.Count > 0)
            {
                _playerCars.Clear();
                _playerCars.AddRange(from distance in _distances
                    group distance by distance.PlayerCar
                    into playerGroup
                    orderby playerGroup.Min(d => d.DistanceSquared) descending
                    select playerGroup.Key);
            }

            while (_playerCars.Count > 0 && _uninitializedAiStates.Count > 0)
            {
                TrafficSplinePoint? spawnPoint = null;
                while (spawnPoint == null && _playerCars.Count > 0)
                {
                    var targetPlayerCar = _playerCars.ElementAt(GetRandomWeighted(_playerCars.Count));
                    _playerCars.Remove(targetPlayerCar);

                    spawnPoint = GetSpawnPoint(targetPlayerCar);
                }

                if (spawnPoint == null || !_mapView.TryNext(spawnPoint, out _))
                    continue;

                foreach (var targetAiState in _uninitializedAiStates)
                {
                    if (!targetAiState.CanSpawn(spawnPoint.Position))
                        continue;
                    
                    targetAiState.Teleport(spawnPoint);

                    _uninitializedAiStates.Remove(targetAiState);
                    break;
                }
            }
        }
    }
}