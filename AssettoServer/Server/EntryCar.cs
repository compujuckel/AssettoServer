using AssettoServer.Network.Packets.Shared;
using AssettoServer.Network.Tcp;
using System;
using System.Numerics;
using System.Threading.Tasks;
using AssettoServer.Server.Configuration;

namespace AssettoServer.Server
{ 
    public partial class EntryCar
    {
        public ACServer Server { get; internal set; }
        public ACTcpClient Client { get; internal set; }
        public CarStatus Status { get; private set; } = new CarStatus();

        public bool ForceLights { get; internal set; }
        public int HighPingSeconds { get; internal set; }

        public long LastActiveTime { get; internal set; }
        public bool HasSentAfkWarning { get; internal set; }
        public bool HasUpdateToSend { get; internal set; }
        public int TimeOffset { get; internal set; }
        public byte SessionId { get; internal set; }
        public uint LastRemoteTimestamp { get; internal set; }
        public int LastPingTime { get; internal set; }
        public int LastPongTime { get; internal set; }
        public ushort Ping { get; internal set; }
        public DriverOptionsFlags DriverOptionsFlags { get; internal set; }

        public bool IsSpectator { get; internal set; }
        public string Model { get; internal set; }
        public string Skin { get; internal set; }
        public int SpectatorMode { get; internal set; }
        public int Ballast { get; internal set; }
        public int Restrictor { get; internal set; }
        
        public float NetworkDistanceSquared { get; internal set; }
        public int OutsideNetworkBubbleUpdateRateMs { get; internal set; }

        internal long[] OtherCarsLastSentUpdateTime { get; set; }
        internal EntryCar TargetCar { get; set; }
        internal long LastFallCheckTime{ get; set;}

        public event EventHandler<EntryCar, PositionUpdateEventArgs> PositionUpdateReceived;
        public event EventHandler<EntryCar, EventArgs> ResetInvoked;

        internal void Reset()
        {
            ResetInvoked?.Invoke(this, EventArgs.Empty);
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
            TargetCar = null;
        }

        internal void CheckAfk()
        {
            if (!Server.Configuration.Extra.EnableAntiAfk || Client?.IsAdministrator == true)
                return;

            long timeAfk = Environment.TickCount64 - LastActiveTime;
            if (timeAfk > Server.Configuration.Extra.MaxAfkTimeMilliseconds)
                _ = Server.KickAsync(Client, Network.Packets.Outgoing.KickReason.Kicked, $"{Client?.Name} has been kicked for being AFK.");
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
            PositionUpdateReceived?.Invoke(this, new PositionUpdateEventArgs { PositionUpdate = positionUpdate});
            
            HasUpdateToSend = true;
            LastRemoteTimestamp = positionUpdate.LastRemoteTimestamp;

            const float afkMinSpeed = 20 / 3.6f;
            if ((positionUpdate.StatusFlag != Status.StatusFlag || positionUpdate.Gas != Status.Gas || positionUpdate.SteerAngle != Status.SteerAngle)
                && (Server.Configuration.Extra.AfkKickBehavior != AfkKickBehavior.MinimumSpeed || positionUpdate.Velocity.LengthSquared() > afkMinSpeed * afkMinSpeed))
            {
                SetActive();
            }
            
            if (Status.Velocity.Y < -75 && Environment.TickCount64 - LastFallCheckTime > 1000)
            {
                LastFallCheckTime = Environment.TickCount64;
                if(Client != null)
                    Server.SendCurrentSession(Client);
            }

            /*if (!AiControlled && Status.StatusFlag != positionUpdate.StatusFlag)
            {
                Log.Debug("Status flag from {0:X} to {1:X}", Status.StatusFlag, positionUpdate.StatusFlag);
            }*/

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

        public bool GetPositionUpdateForCar(EntryCar toCar, out PositionUpdate? positionUpdate)
        {
            var toClient = toCar.Client;
            CarStatus targetCarStatus;
            if (toCar.TargetCar != null)
            {
                targetCarStatus = toCar.TargetCar.AiControlled ? toCar.TargetCar.LastSeenAiState[toCar.SessionId].Status : toCar.TargetCar.Status;
            }
            else
            {
                targetCarStatus = toCar.Status;
            }
            
            if (toCar != this && toClient != null && toClient.HasSentFirstUpdate && (AiControlled || Client != null))
            {
                CarStatus status;
                if (AiControlled)
                {
                    var aiState = GetBestStateForPlayer(targetCarStatus);
                    
                    if (aiState == null)
                    {
                        positionUpdate = null;
                        return false;
                    }

                    if (LastSeenAiState[toCar.SessionId] != aiState
                        || LastSeenAiSpawn[toCar.SessionId] != aiState.SpawnCounter)
                    {
                        LastSeenAiState[toCar.SessionId] = aiState;
                        LastSeenAiSpawn[toCar.SessionId] = aiState.SpawnCounter;

                        if (AiEnableColorChanges)
                        {
                            toClient.SendPacket(new CSPCarColorUpdate
                            {
                                SessionId = SessionId,
                                Color = aiState.Color
                            });
                        }
                    }

                    status = aiState.Status;
                }
                else
                {
                    status = Status;
                }
            
                float distanceSquared = Vector3.DistanceSquared(status.Position, targetCarStatus.Position);
                if (TargetCar != null || distanceSquared > NetworkDistanceSquared)
                {
                    if ((Environment.TickCount64 - OtherCarsLastSentUpdateTime[toCar.SessionId]) < OutsideNetworkBubbleUpdateRateMs)
                    {
                        positionUpdate = null;
                        return false;
                    }

                    OtherCarsLastSentUpdateTime[toCar.SessionId] = Environment.TickCount64;
                }

                positionUpdate = new PositionUpdate
                {
                    SessionId = SessionId,
                    EngineRpm = status.EngineRpm,
                    Gas = status.Gas,
                    Gear = status.Gear,
                    Timestamp = (uint)(status.Timestamp - toCar.TimeOffset),
                    PakSequenceId = AiControlled ? AiPakSequenceIds[toCar.SessionId]++ : status.PakSequenceId,
                    PerformanceDelta = status.PerformanceDelta,
                    Ping = Ping,
                    Position = status.Position,
                    Rotation = status.Rotation,
                    StatusFlag = (Server.Configuration.Extra.ForceLights || ForceLights)
                        ? status.StatusFlag | CarStatusFlags.LightsOn
                        : status.StatusFlag,
                    SteerAngle = status.SteerAngle,
                    TyreAngularSpeedFL = status.TyreAngularSpeed[0],
                    TyreAngularSpeedFR = status.TyreAngularSpeed[1],
                    TyreAngularSpeedRL = status.TyreAngularSpeed[2],
                    TyreAngularSpeedRR = status.TyreAngularSpeed[3],
                    Velocity = status.Velocity,
                    WheelAngle = status.WheelAngle
                };
                return true;
            }

            positionUpdate = null;
            return false;
        }
    }
}
