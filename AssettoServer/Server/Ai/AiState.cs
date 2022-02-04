using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Numerics;
using AssettoServer.Network.Packets.Outgoing;
using JPBotelho;
using Serilog;

namespace AssettoServer.Server.Ai
{
    public class AiState
    {
        public CarStatus Status = new CarStatus();
        public EntryCar EntryCar { get; internal set; }
        public bool Initialized { get; set; }
        [NotNull] public TrafficSplinePoint? CurrentSplinePoint { get; private set; }
        public TrafficMapView MapView { get; private set; }
        public long SpawnProtectionEnds { get; set; }
        public float SafetyDistanceSquared { get; set; } = 20 * 20;
        public float Acceleration { get; set; }
        public float CurrentSpeed { get; private set; }
        public float TargetSpeed { get; private set; }
        public float InitialMaxSpeed { get; private set; }
        public float MaxSpeed { get; private set; }
        public Color Color { get; private set; }
        public byte SpawnCounter { get; private set; }

        private const float WalkingSpeed = 7 / 3.6f;

        private Vector3 _startTangent;
        private Vector3 _endTangent;

        private float _currentVecLength;
        private float _currentVecProgress;
        private long _lastTick = Environment.TickCount64;
        private bool _stoppedForObstacle;
        private long _stoppedForObstacleSince;
        private long _ignoreObstaclesUntil;
        private long _stoppedForCollisionUntil;
        private long _obstacleHonkStart;
        private long _obstacleHonkEnd;

        private static readonly ImmutableList<Color> CarColors = new List<Color>()
        {
            Color.FromArgb(13, 17, 22),
            Color.FromArgb(19, 24, 31),
            Color.FromArgb(28, 29, 33),
            Color.FromArgb(12, 13, 24),
            Color.FromArgb(11, 20, 33),
            Color.FromArgb(151, 154, 151),
            Color.FromArgb(153, 157, 160),
            Color.FromArgb(194, 196, 198 ),
            Color.FromArgb(234, 234, 234),
            Color.FromArgb(255, 255, 255),
            Color.FromArgb(182, 17, 27),
            Color.FromArgb(218, 25, 24),
            Color.FromArgb(73, 17, 29),
            Color.FromArgb(35, 49, 85),
            Color.FromArgb(28, 53, 81),
            Color.FromArgb(37, 58, 167),
            Color.FromArgb(21, 92, 45),
            Color.FromArgb(18, 46, 43),
        }.ToImmutableList();

        public AiState(EntryCar entryCar)
        {
            EntryCar = entryCar;
            MapView = EntryCar.Server.TrafficMap?.NewView() ?? throw new InvalidOperationException("Could not create TrafficMapView");
        }

        private void SetRandomSpeed()
        {
            float variation = EntryCar.Server.Configuration.Extra.AiParams.MaxSpeedMs * EntryCar.Server.Configuration.Extra.AiParams.MaxSpeedVariationPercent;

            float fastLaneOffset = 0;
            if (CurrentSplinePoint.Left != null)
            {
                fastLaneOffset = EntryCar.Server.Configuration.Extra.AiParams.RightLaneOffsetMs;
            }
            InitialMaxSpeed = EntryCar.Server.Configuration.Extra.AiParams.MaxSpeedMs + fastLaneOffset - (variation / 2) + (float)Random.Shared.NextDouble() * variation;
            CurrentSpeed = InitialMaxSpeed;
            TargetSpeed = InitialMaxSpeed;
            MaxSpeed = InitialMaxSpeed;
        }

        private void SetRandomColor()
        {
            Color = CarColors[Random.Shared.Next(CarColors.Count)];
        }

        public void Teleport(TrafficSplinePoint point)
        {
            MapView.Clear();
            CurrentSplinePoint = point;
            if (!MapView.TryNext(CurrentSplinePoint, out var nextPoint))
                throw new InvalidOperationException($"Cannot get next spline point for {CurrentSplinePoint.Id}");
            _currentVecLength = (nextPoint.Point - CurrentSplinePoint.Point).Length();
            _currentVecProgress = 0;
            
            CalculateTangents();
            
            SetRandomSpeed();
            SetRandomColor();
            
            SpawnProtectionEnds = Environment.TickCount64 + Random.Shared.Next(EntryCar.Server.Configuration.Extra.AiParams.MinSpawnProtectionTimeMilliseconds, EntryCar.Server.Configuration.Extra.AiParams.MaxSpawnProtectionTimeMilliseconds);
            SafetyDistanceSquared = Random.Shared.Next(EntryCar.Server.Configuration.Extra.AiParams.MinAiSafetyDistanceSquared, EntryCar.Server.Configuration.Extra.AiParams.MaxAiSafetyDistanceSquared);
            _stoppedForCollisionUntil = 0;
            _ignoreObstaclesUntil = 0;
            _obstacleHonkEnd = 0;
            _obstacleHonkStart = 0;
            _lastTick = Environment.TickCount64;
            SpawnCounter++;
            Initialized = true;
            Update();
        }

        private void CalculateTangents()
        {
            if (!MapView.TryNext(CurrentSplinePoint, out var nextPoint))
                throw new InvalidOperationException("Cannot get next spline point");
            
            if (MapView.TryPrevious(CurrentSplinePoint, out var previousPoint))
            {
                _startTangent = (nextPoint.Point - previousPoint.Point) * 0.5f;
            }
            else
            {
                _startTangent = (nextPoint.Point - CurrentSplinePoint.Point) * 0.5f;
            }

            if (MapView.TryNext(CurrentSplinePoint, out var nextNextPoint, 2))
            {
                _endTangent = (nextNextPoint.Point - CurrentSplinePoint.Point) * 0.5f;
            }
            else
            {
                _endTangent = (nextPoint.Point - CurrentSplinePoint.Point) * 0.5f;
            }
        }

        public bool Move(float progress)
        {
            bool recalculateTangents = false;
            while (progress > _currentVecLength)
            {
                progress -= _currentVecLength;
                
                if (!MapView.TryNext(CurrentSplinePoint, out var nextPoint)
                    || !MapView.TryNext(nextPoint, out var nextNextPoint))
                {
                    Log.Warning("Spline end");
                    return false;
                }

                CurrentSplinePoint = nextPoint;
                _currentVecLength = (nextNextPoint.Point - CurrentSplinePoint.Point).Length();
                recalculateTangents = true;
            }

            if (recalculateTangents)
            {
                CalculateTangents();
            }

            _currentVecProgress = progress;

            return true;
        }

        public bool CanSpawn(Vector3 spawnPoint)
        {
            return EntryCar.CanSpawnAiState(spawnPoint, this);
        }

        private (AiState? ClosestAiState, float ClosestAiStateDistance, float MaxSpeed) SplineLookahead()
        {
            if (!EntryCar.Server.AiEnabled)
                throw new InvalidOperationException("AI disabled");

            float maxCornerBrakingDistance = PhysicsUtils.CalculateBrakingDistance(CurrentSpeed - EntryCar.Server.TrafficMap.MinCorneringSpeed, 
                                                 EntryCar.Server.Configuration.Extra.AiParams.DefaultDeceleration * EntryCar.Server.Configuration.Extra.AiParams.CorneringBrakeForceFactor) 
                                             * EntryCar.Server.Configuration.Extra.AiParams.CorneringBrakeDistanceFactor;
            float maxBrakingDistance = Math.Max(maxCornerBrakingDistance, 50);
            
            AiState? closestAiState = null;
            float closestAiStateDistance = float.MaxValue;
            float distanceTravelled = 0;
            var point = CurrentSplinePoint ?? throw new InvalidOperationException("CurrentSplinePoint is null");
            float maxSpeed = float.MaxValue;
            while (distanceTravelled < maxBrakingDistance)
            {
                distanceTravelled += point.Length;
                point = point.Next;
                if (point == null)
                    break;


                if (closestAiState == null && EntryCar.Server.AiBehavior.AiStatesBySplinePoint.TryGetValue(point, out var candidate))
                {
                    closestAiState = candidate;
                    closestAiStateDistance = Vector3.Distance(Status.Position, closestAiState.Status.Position);
                }

                if (point.MaxCorneringSpeed < CurrentSpeed)
                {
                    float brakingDistance = PhysicsUtils.CalculateBrakingDistance(CurrentSpeed - point.MaxCorneringSpeed,
                                                EntryCar.Server.Configuration.Extra.AiParams.DefaultDeceleration * EntryCar.Server.Configuration.Extra.AiParams.CorneringBrakeForceFactor)
                                            * EntryCar.Server.Configuration.Extra.AiParams.CorneringBrakeDistanceFactor;

                    if (brakingDistance > distanceTravelled)
                    {
                        maxSpeed = Math.Min(point.MaxCorneringSpeed, maxSpeed);
                    }
                }
            }

            return (closestAiState, closestAiStateDistance, maxSpeed);
        }

        private (EntryCar? entryCar, float distance) FindClosestPlayerObstacle()
        {
            EntryCar? closestCar = null;
            float minDistance = float.MaxValue;
            for (var i = 0; i < EntryCar.Server.EntryCars.Count; i++)
            {
                var playerCar = EntryCar.Server.EntryCars[i];
                if (playerCar.Client?.HasSentFirstUpdate == true)
                {
                    float distance = Vector3.DistanceSquared(playerCar.Status.Position, Status.Position);

                    if (distance < minDistance && GetAngleToCar(playerCar.Status) is > 166 and < 194)
                    {
                        minDistance = distance;
                        closestCar = playerCar;
                    }
                }
            }

            if (closestCar != null)
            {
                return (closestCar, (float)Math.Sqrt(minDistance));
            }

            return (null, float.MaxValue);
        }

        private bool IsObstacle(EntryCar playerCar)
        {
            float aiRectWidth = 4; // Lane width
            float halfAiRectWidth = aiRectWidth / 2;
            float aiRectLength = 10; // length of rectangle infront of ai traffic
            float aiRectOffset = 1; // offset of the rectangle from ai position

            float obstacleRectWidth = 1; // width of obstacle car 
            float obstacleRectLength = 1; // length of obstacle car
            float halfObstacleRectWidth = obstacleRectWidth / 2;
            float halfObstanceRectLength = obstacleRectLength / 2;

            Vector3 forward = Vector3.Transform(-Vector3.UnitX, Matrix4x4.CreateRotationY(Status.Rotation.X));
            Matrix4x4 aiViewMatrix = Matrix4x4.CreateLookAt(Status.Position, Status.Position + forward, Vector3.UnitY);

            Matrix4x4 targetWorldViewMatrix = Matrix4x4.CreateRotationY(playerCar.Status.Rotation.X) * Matrix4x4.CreateTranslation(playerCar.Status.Position) * aiViewMatrix;

            Vector3 targetFrontLeft = Vector3.Transform(new Vector3(-halfObstanceRectLength, 0, halfObstacleRectWidth), targetWorldViewMatrix);
            Vector3 targetFrontRight = Vector3.Transform(new Vector3(-halfObstanceRectLength, 0, -halfObstacleRectWidth), targetWorldViewMatrix);
            Vector3 targetRearLeft = Vector3.Transform(new Vector3(halfObstanceRectLength, 0, halfObstacleRectWidth), targetWorldViewMatrix);
            Vector3 targetRearRight = Vector3.Transform(new Vector3(halfObstanceRectLength, 0, -halfObstacleRectWidth), targetWorldViewMatrix);

            static bool isPointInside(Vector3 point, float width, float length, float offset)
                => MathF.Abs(point.X) >= width || (-point.Z >= offset && -point.Z <= offset + length);

            bool isObstacle = isPointInside(targetFrontLeft, halfAiRectWidth, aiRectLength, aiRectOffset)
                              || isPointInside(targetFrontRight, halfAiRectWidth, aiRectLength, aiRectOffset)
                              || isPointInside(targetRearLeft, halfAiRectWidth, aiRectLength, aiRectOffset)
                              || isPointInside(targetRearRight, halfAiRectWidth, aiRectLength, aiRectOffset);

            return isObstacle;
        }

        public void DetectObstacles()
        {
            if (!Initialized) return;
            
            if (Environment.TickCount64 < _ignoreObstaclesUntil)
            {
                SetTargetSpeed(MaxSpeed);
                return;
            }

            if (Environment.TickCount64 < _stoppedForCollisionUntil)
            {
                SetTargetSpeed(0);
                return;
            }
            
            float targetSpeed = InitialMaxSpeed;
            bool hasObstacle = false;

            var splineLookahead = SplineLookahead();
            var playerObstacle = FindClosestPlayerObstacle();

            if (playerObstacle.distance < 10 || splineLookahead.ClosestAiStateDistance < 10)
            {
                targetSpeed = 0;
                hasObstacle = true;
            }
            else if (playerObstacle.distance < splineLookahead.ClosestAiStateDistance && playerObstacle.entryCar != null)
            {
                float playerSpeed = playerObstacle.entryCar.Status.Velocity.Length();

                if (playerSpeed < 0.1f)
                {
                    playerSpeed = 0;
                }

                if ((playerSpeed < CurrentSpeed || playerSpeed == 0)
                    && playerObstacle.distance < PhysicsUtils.CalculateBrakingDistance(CurrentSpeed - playerSpeed, EntryCar.Server.Configuration.Extra.AiParams.DefaultDeceleration) * 2 + 20)
                {
                    targetSpeed = Math.Max(WalkingSpeed, playerSpeed);
                    hasObstacle = true;
                }
            }
            else if (splineLookahead.ClosestAiState != null)
            {
                // AI in front has obstacle
                if (splineLookahead.ClosestAiState.TargetSpeed < splineLookahead.ClosestAiState.MaxSpeed)
                {
                    if ((splineLookahead.ClosestAiState.CurrentSpeed < CurrentSpeed || splineLookahead.ClosestAiState.CurrentSpeed == 0)
                        && splineLookahead.ClosestAiStateDistance < PhysicsUtils.CalculateBrakingDistance(CurrentSpeed - splineLookahead.ClosestAiState.CurrentSpeed, EntryCar.Server.Configuration.Extra.AiParams.DefaultDeceleration) * 2 + 20)
                    {
                        targetSpeed = Math.Max(WalkingSpeed, splineLookahead.ClosestAiState.CurrentSpeed);
                        hasObstacle = true;
                    }
                }
                // AI in front is in clean air, so we just adapt our max speed
                else if(Math.Pow(splineLookahead.ClosestAiStateDistance, 2) < SafetyDistanceSquared)
                {
                    MaxSpeed = Math.Max(WalkingSpeed, splineLookahead.ClosestAiState.CurrentSpeed);
                    targetSpeed = MaxSpeed;
                }
            }

            targetSpeed = Math.Min(splineLookahead.MaxSpeed, targetSpeed);
            
            if (CurrentSpeed == 0 && !_stoppedForObstacle)
            {
                _stoppedForObstacle = true;
                _stoppedForObstacleSince = Environment.TickCount64;
                _obstacleHonkStart = _stoppedForObstacleSince + Random.Shared.Next(3000, 7000);
                _obstacleHonkEnd = _obstacleHonkStart + Random.Shared.Next(500, 1500);
                Log.Verbose("AI {SessionId} stopped for obstacle", EntryCar.SessionId);
            }
            else if (CurrentSpeed > 0 && _stoppedForObstacle)
            {
                _stoppedForObstacle = false;
                Log.Verbose("AI {SessionId} no longer stopped for obstacle", EntryCar.SessionId);
            }
            else if (_stoppedForObstacle && Environment.TickCount64 - _stoppedForObstacleSince > 10_000)
            {
                _ignoreObstaclesUntil = Environment.TickCount64 + 10_000;
                Log.Verbose("AI {SessionId} ignoring obstacles until {IgnoreObstaclesUntil}", EntryCar.SessionId, _ignoreObstaclesUntil);
            }

            float deceleration = EntryCar.Server.Configuration.Extra.AiParams.DefaultDeceleration;
            if (!hasObstacle)
            {
                deceleration *= EntryCar.Server.Configuration.Extra.AiParams.CorneringBrakeForceFactor;
            }
            
            SetTargetSpeed(targetSpeed, deceleration, EntryCar.Server.Configuration.Extra.AiParams.DefaultAcceleration);
        }

        public void StopForCollision()
        {
            _stoppedForCollisionUntil = Environment.TickCount64 + Random.Shared.Next(EntryCar.Server.Configuration.Extra.AiParams.MinCollisionStopTimeMilliseconds, EntryCar.Server.Configuration.Extra.AiParams.MaxCollisionStopTimeMilliseconds);
        }

        public float GetAngleToCar(CarStatus car)
        {
            float challengedAngle = (float) (Math.Atan2(Status.Position.X - car.Position.X, Status.Position.Z - car.Position.Z) * 180 / Math.PI);
            if (challengedAngle < 0)
                challengedAngle += 360;
            float challengedRot = Status.GetRotationAngle();

            challengedAngle += challengedRot;
            challengedAngle %= 360;

            return challengedAngle;
        }

        private void SetTargetSpeed(float speed, float deceleration, float acceleration)
        {
            TargetSpeed = speed;
            if (speed < CurrentSpeed)
            {
                Acceleration = -deceleration;
            }
            else if (speed > CurrentSpeed)
            {
                Acceleration = acceleration;
            }
            else
            {
                Acceleration = 0;
            }
        }

        private void SetTargetSpeed(float speed)
        {
            SetTargetSpeed(speed, EntryCar.Server.Configuration.Extra.AiParams.DefaultDeceleration, EntryCar.Server.Configuration.Extra.AiParams.DefaultAcceleration);
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
            if (!Move(_currentVecProgress + moveMeters) || !MapView.TryNext(CurrentSplinePoint, out var nextPoint))
            {
                Log.Debug("Car {SessionId} reached spline end, despawning", EntryCar.SessionId);
                Initialized = false;
                return;
            }

            CatmullRom.CatmullRomPoint smoothPos = CatmullRom.Evaluate(CurrentSplinePoint.Point, 
                nextPoint.Point, 
                _startTangent, 
                _endTangent, 
                _currentVecProgress / _currentVecLength);

            Vector3 rotation = new Vector3()
            {
                X = (float)(Math.Atan2(smoothPos.Tangent.Z, smoothPos.Tangent.X) - Math.PI / 2),
                Y = (float)(Math.Atan2(new Vector2(smoothPos.Tangent.Z, smoothPos.Tangent.X).Length(), smoothPos.Tangent.Y) - Math.PI / 2) * -1f,
                Z = CurrentSplinePoint.GetCamber(_currentVecProgress / _currentVecLength)
            };

            byte tyreAngularSpeed = (byte) Math.Min(byte.MaxValue, 100 + GetTyreAngularSpeed(CurrentSpeed, 0.65f));

            Status.Timestamp = EntryCar.Server.CurrentTime;
            Status.Position = smoothPos.Position with { Y = smoothPos.Position.Y + EntryCar.AiSplineHeightOffsetMeters };
            Status.Rotation = rotation;
            Status.Velocity = smoothPos.Tangent * CurrentSpeed;
            Status.SteerAngle = 127;
            Status.WheelAngle = 127;
            Status.TyreAngularSpeed[0] = tyreAngularSpeed;
            Status.TyreAngularSpeed[1] = tyreAngularSpeed;
            Status.TyreAngularSpeed[2] = tyreAngularSpeed;
            Status.TyreAngularSpeed[3] = tyreAngularSpeed;
            Status.EngineRpm = (ushort)MathUtils.Lerp(EntryCar.AiIdleEngineRpm, EntryCar.AiMaxEngineRpm, CurrentSpeed / EntryCar.Server.Configuration.Extra.AiParams.MaxSpeedMs);
            Status.StatusFlag = CarStatusFlags.LightsOn
                                | CarStatusFlags.HighBeamsOff
                                | (Environment.TickCount64 < _stoppedForCollisionUntil || CurrentSpeed < 20 / 3.6f ? CarStatusFlags.HazardsOn : 0)
                                | (CurrentSpeed == 0 || Acceleration < 0 ? CarStatusFlags.BrakeLightsOn : 0)
                                | (_stoppedForObstacle && Environment.TickCount64 > _obstacleHonkStart && Environment.TickCount64 < _obstacleHonkEnd ? CarStatusFlags.Horn : 0)
                                | GetWiperSpeed(EntryCar.Server.CurrentWeather.RainIntensity);
            Status.Gear = 2;
        }
        
        private static float GetTyreAngularSpeed(float speed, float wheelDiameter)
        {
            return (float) (speed / (Math.PI * wheelDiameter) * 6);
        }

        private static CarStatusFlags GetWiperSpeed(float rainIntensity)
        {
            return rainIntensity switch
            {
                < 0.05f => 0,
                < 0.25f => CarStatusFlags.WiperLevel1,
                < 0.5f => CarStatusFlags.WiperLevel2,
                _ => CarStatusFlags.WiperLevel3
            };
        }
    }
}