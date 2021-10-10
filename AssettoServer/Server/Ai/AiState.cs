using System;
using System.Linq;
using System.Numerics;
using AssettoServer.Network.Packets.Outgoing;
using AssettoServer.Network.Packets.Shared;
using JPBotelho;
using Serilog;

namespace AssettoServer.Server.Ai
{
    public class AiState
    {
        public CarStatus Status = new CarStatus();
        public EntryCar EntryCar { get; internal set; }
        public bool Initialized { get; private set; }

        public long SpawnProtectionEnds { get; set; }
        public float SafetyDistanceSquared { get; set; } = 20 * 20;
        public float Acceleration { get; set; }
        public float CurrentSpeed { get; private set; }
        public float TargetSpeed { get; private set; }
        public float InitialMaxSpeed { get; private set; }
        public float MaxSpeed { get; private set; }

        private const float WalkingSpeed = 5 / 3.6f;
        
        private Vector3 _startTangent;
        private Vector3 _endTangent;

        private float _currentVecLength;
        private float _currentVecProgress;
        private long _lastTick = Environment.TickCount64;
        private bool _stoppedForObstacle;
        private long _stoppedForObstacleSince;
        private long _ignoreObstaclesUntil;
        private long _obstacleHonkStart;
        private long _obstacleHonkEnd;
        //public TrafficSplinePoint CurrentSplinePoint { get; private set; }
        public TrafficMapView MapView { get; private set; }

        private Random _random = new Random();

        public AiState(EntryCar entryCar)
        {
            EntryCar = entryCar;
            MapView = EntryCar.Server.TrafficMap.NewView();
            SetRandomSpeed();
        }

        public void SetRandomSpeed()
        {
            float variation = EntryCar.Server.Configuration.Extra.AiParams.MaxSpeedMs * EntryCar.Server.Configuration.Extra.AiParams.MaxSpeedVariation;

            float fastLaneOffset = 0;
            if (MapView.CurrentSplinePoint != null && MapView.CurrentSplinePoint.Left != null)
            {
                fastLaneOffset = 10 / 3.6f;
            }
            InitialMaxSpeed = EntryCar.Server.Configuration.Extra.AiParams.MaxSpeedMs + fastLaneOffset - (variation / 2) + (float)_random.NextDouble() * variation;
            CurrentSpeed = InitialMaxSpeed;
            TargetSpeed = InitialMaxSpeed;
            MaxSpeed = InitialMaxSpeed;
        }

        public void Teleport(TrafficSplinePoint point, bool forceUpdate = false)
        {
            if (point == null || point.Next == null)
            {
                return;
            }
            
            MapView.Teleport(point);
            _currentVecLength = (MapView.Peek().Point - MapView.CurrentSplinePoint.Point).Length();
            _currentVecProgress = 0;
            
            CalculateTangents();

            Initialized = true;
            
            if (forceUpdate)
            {
                SetRandomSpeed();
                Update();
            }
        }

        private void CalculateTangents()
        {
            if (MapView.PeekBehind() == null)
            {
                _startTangent = (MapView.Peek().Point - MapView.CurrentSplinePoint.Point) * 0.5f;
            }
            else
            {
                _startTangent = (MapView.CurrentSplinePoint.Point - MapView.PeekBehind().Point) * 0.5f;
            }

            if (MapView.Peek(2) == null)
            {
                _endTangent = (MapView.Peek().Point - MapView.CurrentSplinePoint.Point) * 0.5f;
            }
            else
            {
                _endTangent = (MapView.Peek(2).Point - MapView.CurrentSplinePoint.Point) * 0.5f;
            }
        }

        public bool Move(float progress)
        {
            while (progress > _currentVecLength)
            {
                progress -= _currentVecLength;
                
                if (MapView.Peek(2) == null)
                {
                    Log.Warning("Spline end");
                    return false;
                }
                
                MapView.Traverse();
                _currentVecLength = (MapView.Peek().Point - MapView.CurrentSplinePoint.Point).Length();
            }

            CalculateTangents();

            _currentVecProgress = progress;

            return true;
        }

        public bool CanSpawn(Vector3 spawnPoint)
        {
            return EntryCar.CanSpawnAiState(spawnPoint, this);
        }

        private (AiState aiState, float distance) FindClosestAiObstacle()
        {
            var aiStates = EntryCar.Server.EntryCars
                .Where(car => car.AiControlled)
                .SelectMany(car => car.GetAiStatesCopy())
                .Where(state => Vector3.DistanceSquared(Status.Position, state.Status.Position) < 250 * 250);

            AiState closestState = null;
            int minDistance = int.MaxValue;
            foreach (var aiState in aiStates)
            {
                if(aiState == this) continue;
                
                var point = MapView.CurrentSplinePoint;
                for (int distance = 0; distance < 100 && point != null; distance++)
                {
                    if (point == aiState.MapView.CurrentSplinePoint && distance < minDistance)
                    {
                        minDistance = distance;
                        closestState = aiState;
                        break;
                    }

                    point = MapView.Peek(distance);
                }
            }

            if (closestState != null)
            {
                float distance = Vector3.Distance(Status.Position, closestState.Status.Position);
                return (closestState, distance);
            }
            
            return (null, float.MaxValue);
        }

        private (EntryCar entryCar, float distance) FindClosestPlayerObstacle()
        {
            var playerCars = EntryCar.Server.EntryCars
                .Where(car => car.Client != null && car.Client.HasSentFirstUpdate);

            EntryCar closestCar = null;
            float minDistance = float.MaxValue;
            foreach (var playerCar in playerCars)
            {
                float distance = Vector3.DistanceSquared(playerCar.Status.Position, Status.Position);

                if (distance < minDistance && GetAngleToCar(playerCar.Status) is > 165 and < 195)
                {
                    minDistance = distance;
                    closestCar = playerCar;
                }
            }

            if (closestCar != null)
            {
                return (closestCar, (float)Math.Sqrt(minDistance));
            }

            return (null, float.MaxValue);
        }

        public void DetectObstacles()
        {
            if (Environment.TickCount64 < _ignoreObstaclesUntil)
            {
                SetTargetSpeed(MaxSpeed);
                return;
            }

            float targetSpeed = InitialMaxSpeed;

            var aiObstacle = FindClosestAiObstacle();
            var playerObstacle = FindClosestPlayerObstacle();

            if (playerObstacle.distance < 15 || aiObstacle.distance < 15)
            {
                targetSpeed = 0;
            }
            else if (playerObstacle.distance < aiObstacle.distance && playerObstacle.entryCar != null)
            {
                float playerSpeed = playerObstacle.entryCar.Status.Velocity.Length();

                if (playerSpeed < 0.1f)
                {
                    playerSpeed = 0;
                }

                if ((playerSpeed < CurrentSpeed || playerSpeed == 0)
                    && playerObstacle.distance < GetBrakingDistance(playerSpeed) * 2 + 20)
                {
                    targetSpeed = Math.Max(WalkingSpeed, playerSpeed);
                }
            }
            else if (aiObstacle.aiState != null)
            {
                // AI in front has obstacle
                if (aiObstacle.aiState.TargetSpeed < aiObstacle.aiState.MaxSpeed)
                {
                    if ((aiObstacle.aiState.CurrentSpeed < CurrentSpeed || aiObstacle.aiState.CurrentSpeed == 0)
                        && aiObstacle.distance < GetBrakingDistance(aiObstacle.aiState.CurrentSpeed) * 2 + 20)
                    {
                        targetSpeed = Math.Max(WalkingSpeed, aiObstacle.aiState.CurrentSpeed);
                    }
                }
                // AI in front is in clean air, so we just adapt our max speed
                else if(Math.Pow(aiObstacle.distance, 2) < SafetyDistanceSquared)
                {
                    MaxSpeed = Math.Max(WalkingSpeed, aiObstacle.aiState.CurrentSpeed);
                    targetSpeed = MaxSpeed;
                }
            }
            
            if (CurrentSpeed == 0 && !_stoppedForObstacle)
            {
                _stoppedForObstacle = true;
                _stoppedForObstacleSince = Environment.TickCount64;
                _obstacleHonkStart = _stoppedForObstacleSince + _random.Next(3000, 7000);
                _obstacleHonkEnd = _obstacleHonkStart + _random.Next(500, 1500);
                Log.Debug("AI {0} stopped for obstacle", EntryCar.SessionId);
            }
            else if (CurrentSpeed > 0 && _stoppedForObstacle)
            {
                _stoppedForObstacle = false;
                Log.Debug("AI {0} no longer stopped for obstacle", EntryCar.SessionId);
            }
            else if (_stoppedForObstacle && Environment.TickCount64 - _stoppedForObstacleSince > 10_000)
            {
                _ignoreObstaclesUntil = Environment.TickCount64 + 10_000;
                Log.Debug("AI {0} ignoring obstacles until {1}", EntryCar.SessionId, _ignoreObstaclesUntil);
            }
            
            SetTargetSpeed(targetSpeed);
        }

        private float GetAngleToCar(CarStatus car)
        {
            float challengedAngle = (float) (Math.Atan2(Status.Position.X - car.Position.X, Status.Position.Z - car.Position.Z) * 180 / Math.PI);
            if (challengedAngle < 0)
                challengedAngle += 360;
            float challengedRot = Status.GetRotationAngle();

            challengedAngle += challengedRot;
            challengedAngle %= 360;

            return challengedAngle;
        }

        private float GetBrakingDistance(float targetSpeed)
        {
            return (float) Math.Abs(Math.Pow(targetSpeed - CurrentSpeed, 2) / (2 * EntryCar.Server.Configuration.Extra.AiParams.DefaultDeceleration));
        }

        private float GetTyreAngularSpeed(float speed, float wheelDiameter)
        {
            return (float) (speed / (Math.PI * wheelDiameter) * 6);
        }

        private void SetTargetSpeed(float speed)
        {
            TargetSpeed = speed;
            if (speed < CurrentSpeed)
            {
                Acceleration = EntryCar.Server.Configuration.Extra.AiParams.DefaultDeceleration;
            }
            else if(speed > CurrentSpeed)
            {
                Acceleration = EntryCar.Server.Configuration.Extra.AiParams.DefaultAcceleration;
            }
            else
            {
                Acceleration = 0;
            }
        }

        public void Update()
        {
            if (!Initialized)
                return;
            
            long dt = Environment.TickCount64 - _lastTick;
            _lastTick = Environment.TickCount64;

            if (Acceleration != 0)
            {
                CurrentSpeed += Acceleration * (dt / 1000.0f);
                
                if ((Acceleration < 0 && CurrentSpeed < TargetSpeed) || (Acceleration > 0 && CurrentSpeed > TargetSpeed))
                {
                    CurrentSpeed = TargetSpeed;
                    Acceleration = 0;
                }
            }

            float moveMeters = (dt / 1000.0f) * CurrentSpeed;
            if (!Move(_currentVecProgress + moveMeters))
            {
                Log.Debug("Car {0} reached spline end, respawning", EntryCar.SessionId);
                Teleport(EntryCar.Server.TrafficMap.Splines[0].Points[0]);
            }

            CatmullRom.CatmullRomPoint smoothPos = CatmullRom.Evaluate(MapView.CurrentSplinePoint.Point, 
                MapView.Peek().Point, 
                _startTangent, 
                _endTangent, 
                _currentVecProgress / _currentVecLength);

            Vector3 rotation = new Vector3()
            {
                X = (float)(Math.Atan2(smoothPos.Tangent.Z, smoothPos.Tangent.X) - Math.PI / 2),
                Y = (float)(Math.Atan2(new Vector2(smoothPos.Tangent.Z, smoothPos.Tangent.X).Length(), smoothPos.Tangent.Y) - Math.PI / 2) * -1f,
                Z = MapView.CurrentSplinePoint.GetBankAngle(_currentVecProgress / _currentVecLength)
            };

            byte tyreAngularSpeed = (byte) Math.Min(byte.MaxValue, 100 + GetTyreAngularSpeed(CurrentSpeed, 0.65f));

            UpdatePosition(new PositionUpdate()
            {
                Timestamp = (uint)(Environment.TickCount - EntryCar.Server.StartTime),
                LastRemoteTimestamp = (uint)(Environment.TickCount - EntryCar.Server.StartTime),
                Position = smoothPos.Position,
                Rotation = rotation,
                Velocity = smoothPos.Tangent * CurrentSpeed,
                SteerAngle = 127,
                WheelAngle = 127,
                TyreAngularSpeedFL = tyreAngularSpeed,
                TyreAngularSpeedFR = tyreAngularSpeed,
                TyreAngularSpeedRL = tyreAngularSpeed,
                TyreAngularSpeedRR = tyreAngularSpeed,
                EngineRpm = (ushort) MathUtils.Lerp(800, 3000, CurrentSpeed / EntryCar.Server.Configuration.Extra.AiParams.MaxSpeedMs),
                StatusFlag = CarStatusFlags.LightsOn
                             | CarStatusFlags.HighBeamsOff
                             | (CurrentSpeed < 20 / 3.6f ? CarStatusFlags.HazardsOn : 0)
                             | (CurrentSpeed == 0 || Acceleration < 0 ? CarStatusFlags.BrakeLightsOn : 0)
                             | (_stoppedForObstacle && Environment.TickCount64 > _obstacleHonkStart && Environment.TickCount64 < _obstacleHonkEnd ? CarStatusFlags.Horn : 0),
                Gear = 2
            });
        }

        private void UpdatePosition(PositionUpdate positionUpdate)
        {
            Status.Timestamp = positionUpdate.LastRemoteTimestamp;
            Status.PakSequenceId = positionUpdate.PakSequenceId;
            Status.Position = positionUpdate.Position;
            Status.Rotation = positionUpdate.Rotation;
            Status.Velocity = positionUpdate.Velocity;
            Status.TyreAngularSpeed[0] = positionUpdate.TyreAngularSpeedFL;
            Status.TyreAngularSpeed[1] = positionUpdate.TyreAngularSpeedFR;
            Status.TyreAngularSpeed[2] = positionUpdate.TyreAngularSpeedRL;
            Status.TyreAngularSpeed[3] = positionUpdate.TyreAngularSpeedRR;
            Status.SteerAngle = positionUpdate.SteerAngle;
            Status.WheelAngle = positionUpdate.WheelAngle;
            Status.EngineRpm = positionUpdate.EngineRpm;
            Status.Gear = positionUpdate.Gear;
            Status.StatusFlag = positionUpdate.StatusFlag;
            Status.PerformanceDelta = positionUpdate.PerformanceDelta;
            Status.Gas = positionUpdate.Gas;
            Status.NormalizedPosition = positionUpdate.NormalizedPosition;
        }
    }
}