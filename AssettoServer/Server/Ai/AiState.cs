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

        public long SpawnProtectionEnds { get; set; }
        public float SafetyDistanceSquared { get; set; } = 20 * 20;
        public float Acceleration { get; set; }
        public float CurrentSpeed { get; private set; }
        public float TargetSpeed { get; private set; }
        
        private Vector3 _startTangent;
        private Vector3 _endTangent;

        private float _currentVecLength;
        private float _currentVecProgress;
        private bool _initialized = false;
        private long _lastTick = Environment.TickCount64;
        private bool _stoppedForObstacle;
        private long _stoppedForObstacleSince;
        private long _ignoreObstaclesUntil;
        private long _obstacleHonkStart;
        private long _obstacleHonkEnd;
        private TrafficSplinePoint _currentPoint;

        private Random _random = new Random();

        public AiState(EntryCar entryCar)
        {
            EntryCar = entryCar;
            CurrentSpeed = EntryCar.Server.Configuration.Extra.AiParams.MaxSpeedMs;
            TargetSpeed = EntryCar.Server.Configuration.Extra.AiParams.MaxSpeedMs;
        }
        
        public void Teleport(TrafficSplinePoint point)
        {
            if (point == null || point.Next == null)
            {
                return;
            }

            _currentPoint = point;
            _currentVecLength = (_currentPoint.Next.Point - _currentPoint.Point).Length();
            _currentVecProgress = 0;
            
            CalculateTangents();
        }

        private void CalculateTangents()
        {
            if (_currentPoint.Previous == null)
            {
                _startTangent = (_currentPoint.Next.Point - _currentPoint.Point) * 0.5f;
            }
            else
            {
                _startTangent = (_currentPoint.Point - _currentPoint.Previous.Point) * 0.5f;
            }

            if (_currentPoint.Next.Next == null)
            {
                _endTangent = (_currentPoint.Next.Point - _currentPoint.Point) * 0.5f;
            }
            else
            {
                _endTangent = (_currentPoint.Next.Next.Point - _currentPoint.Point) * 0.5f;
            }
        }

        public bool Move(float progress)
        {
            while (progress > _currentVecLength)
            {
                progress -= _currentVecLength;
                
                if (_currentPoint.Next == null 
                    || _currentPoint.Next.Next == null)
                {
                    Log.Warning("Spline end");
                    return false;
                }
                
                _currentPoint = _currentPoint.Next;
                _currentVecLength = (_currentPoint.Next.Point - _currentPoint.Point).Length();
            }

            CalculateTangents();

            _currentVecProgress = progress;

            return true;
        }

        public void DetectObstacles()
        {
            if (Environment.TickCount64 < _ignoreObstaclesUntil)
            {
                SetTargetSpeed(EntryCar.Server.Configuration.Extra.AiParams.MaxSpeedMs);
                return;
            }

            float minSpeed = EntryCar.Server.Configuration.Extra.AiParams.MaxSpeedMs;
            bool hasObstacle = false;

            var playerCars = EntryCar.Server.EntryCars.Where(car => car.Client != null && car.Client.HasSentFirstUpdate).Select(car => car.Status);
            var aiCars = EntryCar.Server.EntryCars.Where(car => car.AiControlled).SelectMany(car => car.AiStates).Select(state => state.Status);

            var all = playerCars.Concat(aiCars);
            
            //foreach (var car in EntryCar.Server.EntryCars.Where(car => car.AiControlled || (car.Client != null && car.Client.HasSentFirstUpdate)))
            foreach (var carStatus in all)
            {
                if (carStatus == Status) continue;

                float carSpeed = carStatus.Velocity.Length();
                float distance = Vector3.Distance(carStatus.Position, Status.Position);

                if (carSpeed < 0.1f)
                {
                    carSpeed = 0;
                }

                // Check first if car is in front of us
                if (GetAngleToCar(carStatus) is > 165 and < 195)
                {
                    // always brake if the distance is too small
                    if (distance < 10)
                    {
                        minSpeed = 0;
                        hasObstacle = true;
                    }
                    else
                    {
                        // Make full stop if speed is too low. Someone might be trying to turn around
                        if (carSpeed < 20 / 3.6f)
                        {
                            carSpeed = 5 / 3.6f;
                            //carSpeed = Math.Clamp(carSpeed, 0, 5 / 3.6f);
                        }
                        
                        if ((carSpeed + 1 < CurrentSpeed || carSpeed == 0)
                            && distance < GetBrakingDistance(carSpeed) * 1.5 + 20)
                        {
                            minSpeed = Math.Min(minSpeed, Math.Max(5 / 3.6f, carSpeed));
                            hasObstacle = true;
                        }
                    }
                }
            }
            
            if (CurrentSpeed == 0 && !_stoppedForObstacle && hasObstacle)
            {
                _stoppedForObstacle = true;
                _stoppedForObstacleSince = Environment.TickCount64;
                _obstacleHonkStart = _stoppedForObstacleSince + _random.Next(3000, 7000);
                _obstacleHonkEnd = _obstacleHonkStart + _random.Next(500, 1500);
                Log.Debug("AI {0} stopped for obstacle", EntryCar.SessionId);
            }
            else if (_stoppedForObstacle && !hasObstacle)
            {
                _stoppedForObstacle = false;
                Log.Debug("AI {0} no longer stopped for obstacle", EntryCar.SessionId);
            }

            if (_stoppedForObstacle && Environment.TickCount64 - _stoppedForObstacleSince > 10_000)
            {
                _ignoreObstaclesUntil = Environment.TickCount64 + 10_000;
                Log.Debug("AI {0} ignoring obstacles until {1}", EntryCar.SessionId, _ignoreObstaclesUntil);
            }
            
            SetTargetSpeed(minSpeed);
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
            if (!_initialized) // TODO remove?
            {
                int spawnPos = new Random().Next(0, EntryCar.Server.TrafficMap.Splines[0].Points.Length);
                Teleport(EntryCar.Server.TrafficMap.Splines[0].Points[spawnPos]);
                _initialized = true;
            }
            
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

            CatmullRom.CatmullRomPoint smoothPos = CatmullRom.Evaluate(_currentPoint.Point, 
                _currentPoint.Next.Point, 
                _startTangent, 
                _endTangent, 
                _currentVecProgress / _currentVecLength);
                
            Vector3 rotation = new Vector3()
            {
                X = (float)(Math.Atan2(smoothPos.Tangent.Z, smoothPos.Tangent.X) - Math.PI / 2),
                Y = (float)(Math.Atan2(new Vector2(smoothPos.Tangent.Z, smoothPos.Tangent.X).Length(), smoothPos.Tangent.Y) - Math.PI / 2) * -1f,
                Z = 0
            };

            byte tyreAngularSpeed = (byte) Math.Min(byte.MaxValue, 100 + GetTyreAngularSpeed(CurrentSpeed, 0.65f));

            UpdatePosition(new PositionUpdate()
            {
                PakSequenceId = (byte)(Status.PakSequenceId + 1),
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