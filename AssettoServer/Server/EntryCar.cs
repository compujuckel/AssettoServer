using AssettoServer.Network.Packets;
using AssettoServer.Network.Packets.Incoming;
using AssettoServer.Network.Packets.Shared;
using AssettoServer.Network.Tcp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using AssettoServer.Network.Packets.Outgoing;
using Serilog;

namespace AssettoServer.Server
{
    public enum AiMode
    {
        Disabled,
        Auto,
        Fixed
    }
    
    public class EntryCar
    {
        public ACServer Server { get; internal set; }
        public ACTcpClient Client { get; internal set; }
        public CarStatus Status { get; private set; } = new CarStatus();

        public bool ForceLights { get; internal set; }
        public int HighPingSeconds { get; internal set; }
        public int LightFlashCount { get; internal set; }

        public long LastActiveTime { get; internal set; }
        public bool HasSentAfkWarning { get; internal set; }
        public bool HasUpdateToSend { get; internal set; }
        public int TimeOffset { get; internal set; }
        public byte SessionId { get; internal set; }
        public uint LastRemoteTimestamp { get; internal set; }
        public int LastPingTime { get; internal set; }
        public int LastPongTime { get; internal set; }
        public ushort Ping { get; internal set; }

        public bool IsSpectator { get; internal set; }
        public string Model { get; internal set; }
        public string Skin { get; internal set; }
        public int SpectatorMode { get; internal set; }
        public int Ballast { get; internal set; }
        public int Restrictor { get; internal set; }

        internal long[] OtherCarsLastSentUpdateTime { get; set; }
        internal Race CurrentRace { get; set; }
        internal EntryCar TargetCar { get; set; }
        internal long LastFallCheckTime{ get; set;}

        private long LastLightFlashTime { get; set; }
        private long LastRaceChallengeTime { get; set; }
        
        public long AiSpawnProtectionEnds { get; set; }
        public bool AiControlled { get; set; }
        public AiMode AiMode { get; set; }

        private const float AiSpeed = 80 / 3.6f;

        private Vector3 _aiCurrentVec;
        private Vector3 _aiCurrentVecNormal;
        private Vector3 _aiCurrentVecProgress;
        private int _aiSplinePosition;
        private bool _aiInitialized = false;
        private long _aiLastTick = Environment.TickCount64;

        public void AiMoveToSplinePosition(int splinePos, bool forceUpdate = false)
        {
            _aiSplinePosition = splinePos;
            Vector3 currentPos = Server.AiSpline.IdealLine[splinePos].Pos;
            Vector3 nextPos = Server.AiSpline.IdealLine[(splinePos + 1) % Server.AiSpline.IdealLine.Length].Pos;
            _aiCurrentVec = Vector3.Subtract(nextPos, currentPos);
            _aiCurrentVecNormal = Vector3.Normalize(_aiCurrentVec);
            _aiCurrentVecProgress = Vector3.Zero;

            if (forceUpdate)
            {
                AiUpdate();
            }
        }

        public void SetAiControl(bool aiControlled)
        {
            if (AiControlled != aiControlled)
            {
                AiControlled = aiControlled;

                if (AiControlled)
                {
                    Log.Debug("Slot {0} is now controlled by AI", SessionId);
                    Reset();
                    Server.BroadcastPacket(new CarConnected
                    {
                        SessionId = SessionId,
                        Name = $"Traffic {SessionId}"
                    });
                }
                else
                {
                    Log.Debug("Slot {0} is no longer controlled by AI", SessionId);
                    Server.BroadcastPacket(new CarDisconnected { SessionId = SessionId });
                }
            }
        }

        public void AiUpdate()
        {
            if (!_aiInitialized) // TODO remove?
            {
                _aiSplinePosition = new Random().Next(0, Server.AiSpline.IdealLine.Length);
                
                AiMoveToSplinePosition(_aiSplinePosition);
                _aiInitialized = true;
            }
            
            long dt = Environment.TickCount64 - _aiLastTick;
            _aiLastTick = Environment.TickCount64;

            float moveMeters = (dt / 1000.0f) * AiSpeed;
            
            _aiCurrentVecProgress += _aiCurrentVecNormal * moveMeters;
            
            //Log.Debug("dt {0} m {1} cur {2} prog {3} pl {4} cl {5}", dt, moveMeters, _currentVec, _currentVecProgress, _currentVecProgress.Length(), _currentVec.Length());
            if (_aiCurrentVecProgress.Length() > _aiCurrentVec.Length())
            {
                _aiSplinePosition++;
                if (_aiSplinePosition >= Server.AiSpline.IdealLine.Length)
                {
                    _aiSplinePosition = 0;
                }
                //Log.Debug("next spline pos {0}", _aiSplinePosition);
                
                AiMoveToSplinePosition(_aiSplinePosition);
            }
            
            Vector3 rotation = new Vector3()
            {
                X = (float)(Math.Atan2(_aiCurrentVec.Z, _aiCurrentVec.X) - Math.PI / 2),
                //Y = (float)(Math.Acos(_currentVecNormal.Y))
                //Z = (float)Math.Acos(direction.Z)
            };

            //Log.Debug("cur {0}, nxt {1}, rot {2}", currentPos, nextPos, rotation);

            UpdatePosition(new PositionUpdate()
            {
                PakSequenceId = (byte)(Status.PakSequenceId + 1),
                Timestamp = (uint)(Environment.TickCount - Server.StartTime),
                LastRemoteTimestamp = (uint)(Environment.TickCount - Server.StartTime),
                Position = Server.AiSpline.IdealLine[_aiSplinePosition].Pos + _aiCurrentVecProgress,
                Rotation = rotation,
                Velocity = Vector3.Multiply(Vector3.Normalize(_aiCurrentVec), AiSpeed),
                SteerAngle = 127,
                WheelAngle = 127,
                TyreAngularSpeedFL = 100,
                TyreAngularSpeedFR = 100,
                TyreAngularSpeedRL = 100,
                TyreAngularSpeedRR = 100,
                EngineRpm = 3000
            });
        }

        internal void Reset()
        {
            IsSpectator = false;
            SpectatorMode = 0;
            LastActiveTime = 0;
            HasSentAfkWarning = false;
            HasUpdateToSend = false;
            TimeOffset = 0;
            LastRemoteTimestamp = 0;
            HighPingSeconds = 0;
            LastPingTime = 0;
            Ping = 0;
            ForceLights = false;
            Status = new CarStatus();
            CurrentRace = null;
            TargetCar = null;
        }

        internal void CheckAfk()
        {
            if (!Server.Configuration.Extra.EnableAntiAfk || Client?.IsAdministrator == true)
                return;

            long timeAfk = Environment.TickCount64 - LastActiveTime;
            if (timeAfk > Server.Configuration.Extra.MaxAfkTimeMilliseconds)
                _ = Server.KickAsync(Client, Network.Packets.Outgoing.KickReason.None, $"{Client?.Name} has been kicked for being AFK.");
            else if (!HasSentAfkWarning && Server.Configuration.Extra.MaxAfkTimeMilliseconds - timeAfk < 60000)
            {
                HasSentAfkWarning = true;
                Client?.SendPacket(new ChatMessage { SessionId = 255, Message = "You will be kicked in 1 minute for being AFK." });
            }
        }

        internal void SetActive()
        {
            LastActiveTime = Environment.TickCount64;
            HasSentAfkWarning = false;
        }

        internal void UpdatePosition(PositionUpdate positionUpdate)
        {
            HasUpdateToSend = true;
            LastRemoteTimestamp = positionUpdate.LastRemoteTimestamp;

            if (positionUpdate.StatusFlag != Status.StatusFlag || positionUpdate.Gas != Status.Gas || positionUpdate.SteerAngle != Status.SteerAngle)
                SetActive();

            long currentTick = Environment.TickCount64;
            if(((Status.StatusFlag & 0x20) == 0 && (positionUpdate.StatusFlag & 0x20) != 0) || ((Status.StatusFlag & 0x4000) == 0 && (positionUpdate.StatusFlag & 0x4000) != 0))
            {
                LastLightFlashTime = currentTick;
                LightFlashCount++;
            }

            if ((Status.StatusFlag & 0x2000) == 0 && (positionUpdate.StatusFlag & 0x2000) != 0)
            {
                if (CurrentRace != null && !CurrentRace.HasStarted && !CurrentRace.LineUpRequired)
                    _ = CurrentRace.StartAsync();
            }

            if (currentTick - LastLightFlashTime > 3000 && LightFlashCount > 0)
            {
                LightFlashCount = 0;
            }

            if (LightFlashCount == 3)
            {
                LightFlashCount = 0;

                if(currentTick - LastRaceChallengeTime > 20000)
                {
                    Task.Run(ChallengeNearbyCar);
                    LastRaceChallengeTime = currentTick;
                }
            }


            Status.Timestamp = LastRemoteTimestamp + TimeOffset;
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

        internal void ChallengeCar(EntryCar car, bool lineUpRequired = true)
        {
            void Reply(string message)
                => Client.SendPacket(new ChatMessage { SessionId = 255, Message = message });

            Race currentRace = CurrentRace;
            if (currentRace != null)
            {
                if (currentRace.HasStarted)
                    Reply("You are currently in a race.");
                else
                    Reply("You have a pending race request.");
            }
            else
            {
                if (car == this)
                    Reply("You cannot challenge yourself to a race.");
                else
                {
                    currentRace = car.CurrentRace;
                    if (currentRace != null)
                    {
                        if (currentRace.HasStarted)
                            Reply("This car is currently in a race.");
                        else
                            Reply("This car has a pending race request.");
                    }
                    else
                    {
                        currentRace = new Race(Server, this, car, lineUpRequired);
                        CurrentRace = currentRace;
                        car.CurrentRace = currentRace;

                        Client.SendPacket(new ChatMessage { SessionId = 255, Message = $"You have challenged {car.Client.Name} to a race." });

                        if (lineUpRequired)
                            car.Client.SendPacket(new ChatMessage { SessionId = 255, Message = $"{Client.Name} has challenged you to a race. Send /accept within 10 seconds to accept." });
                        else
                            car.Client.SendPacket(new ChatMessage { SessionId = 255, Message = $"{Client.Name} has challenged you to a race. Flash your hazard lights or send /accept within 10 seconds to accept." });

                        _ = Task.Delay(10000).ContinueWith(t =>
                        {
                            if (!currentRace.HasStarted)
                            {
                                CurrentRace = null;
                                car.CurrentRace = null;

                                ChatMessage timeoutMessage = new ChatMessage { SessionId = 255, Message = $"Race request has timed out." };
                                Client.SendPacket(timeoutMessage);
                                car.Client.SendPacket(timeoutMessage);
                            }
                        });
                    }
                }
            }
        }

        private void ChallengeNearbyCar()
        {
            EntryCar bestMatch = null;
            float distanceSquared = 30 * 30;

            foreach(EntryCar car in Server.EntryCars)
            {
                ACTcpClient carClient = car.Client;
                if(carClient != null && car != this)
                {
                    float challengedAngle = (float)(Math.Atan2(Status.Position.X - car.Status.Position.X, Status.Position.Z - car.Status.Position.Z) * 180 / Math.PI);
                    if (challengedAngle < 0)
                        challengedAngle += 360;
                    float challengedRot = car.Status.GetRotationAngle();

                    challengedAngle += challengedRot;
                    challengedAngle %= 360;

                    if (challengedAngle > 110 && challengedAngle < 250 && Vector3.DistanceSquared(car.Status.Position, Status.Position) < distanceSquared)
                        bestMatch = car;
                }
            }

            if (bestMatch != null)
                ChallengeCar(bestMatch, false);
        }
    }

    public class CarStatus
    {
        public float[] DamageZoneLevel { get; } = new float[5];
        public short P2PCount { get; internal set; }
        public bool P2PActive { get; internal set; }
        public bool MandatoryPit { get; internal set; }
        public string CurrentTyreCompound { get; internal set; }

        public byte PakSequenceId { get; internal set; }
        public Vector3 Position { get; internal set; }
        public Vector3 Rotation { get; internal set; }
        public Vector3 Velocity { get; internal set; }
        public long Timestamp { get; internal set; }
        public byte[] TyreAngularSpeed { get; } = new byte[4];
        public byte SteerAngle { get; internal set; }
        public byte WheelAngle { get; internal set; }
        public ushort EngineRpm { get; internal set; }
        public byte Gear { get; internal set; }
        public uint StatusFlag { get; internal set; }
        public short PerformanceDelta { get; internal set; }
        public byte Gas { get; internal set; }
        public float NormalizedPosition { get; internal set; }

        public float GetRotationAngle()
        {
            float angle = (float)(Rotation.X * 180 / Math.PI);
            if (angle < 0)
                angle += 360;

            return angle;
        }

        public float GetVelocityAngle()
        {
            if (Math.Abs(Velocity.X) < 1 && Math.Abs(Velocity.Z) < 1)
                return GetRotationAngle();

            Vector3 normalizedVelocity = Vector3.Normalize(Velocity);
            float angle = (float)-(Math.Atan2(normalizedVelocity.X, normalizedVelocity.Z) * 180 / Math.PI);
            if (angle < 0)
                angle += 360;

            return angle;
        }
    }
}
