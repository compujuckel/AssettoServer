using AssettoServer.Network.Packets.Shared;
using AssettoServer.Network.Tcp;
using System;
using System.Numerics;
using AssettoServer.Network.Packets.Incoming;
using AssettoServer.Network.Packets.Outgoing;
using AssettoServer.Server.Ai;
using AssettoServer.Server.Configuration;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace AssettoServer.Server
{ 
    public partial class EntryCar
    {
        public EntryCar(string model, string? skin, ACServer server, byte sessionId)
        {
            Model = model;
            Skin = skin ?? "";
            Server = server;
            SessionId = sessionId;
            OtherCarsLastSentUpdateTime = new long[Server.EntryCars.Length];

            AiPakSequenceIds = new byte[Server.EntryCars.Length];
            LastSeenAiState = new AiState[Server.EntryCars.Length];
            LastSeenAiSpawn = new byte[Server.EntryCars.Length];
            
            AiInit();
        }

        public ACServer Server { get; }
        public ACTcpClient? Client { get; internal set; }
        public CarStatus Status { get; private set; } = new CarStatus();

        public bool ForceLights { get; internal set; }
        public int HighPingSeconds { get; internal set; }

        public long LastActiveTime { get; internal set; }
        public bool HasSentAfkWarning { get; internal set; }
        public bool HasUpdateToSend { get; internal set; }
        public int TimeOffset { get; internal set; }
        public byte SessionId { get; }
        public uint LastRemoteTimestamp { get; internal set; }
        public int LastPingTime { get; internal set; }
        public int LastPongTime { get; internal set; }
        public ushort Ping { get; internal set; }
        public DriverOptionsFlags DriverOptionsFlags { get; internal set; }

        public bool IsSpectator { get; internal set; }
        public string Model { get; }
        public string Skin { get; }
        public int SpectatorMode { get; internal set; }
        public int Ballast { get; internal set; }
        public int Restrictor { get; internal set; }
        
        public float NetworkDistanceSquared { get; internal set; }
        public int OutsideNetworkBubbleUpdateRateMs { get; internal set; }

        internal long[] OtherCarsLastSentUpdateTime { get; }
        internal EntryCar? TargetCar { get; set; }
        private long LastFallCheckTime{ get; set; }
        
        /// <summary>
        /// Fires when a position update is received.
        /// </summary>
        public event EventHandlerIn<EntryCar, PositionUpdateIn>? PositionUpdateReceived;
        
        /// <summary>
        /// Fires when the state of this car is reset, usually when a new player connects.
        /// </summary>
        public event EventHandler<EntryCar, EventArgs>? ResetInvoked;

        public ILogger Logger { get; private set; } = Log.Logger;

        public class EntryCarLogEventEnricher : ILogEventEnricher
        {
            private readonly EntryCar _entryCar;

            public EntryCarLogEventEnricher(EntryCar entryCar)
            {
                _entryCar = entryCar;
            }
            
            public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SessionId", _entryCar.SessionId));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("CarModel", _entryCar.Model));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("CarSkin", _entryCar.Skin));
            }
        }

        public void ResetLogger()
        {
            Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.With(new EntryCarLogEventEnricher(this))
                .WriteTo.Logger(Log.Logger)
                .CreateLogger();
        }
        
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

        internal void UpdatePosition(in PositionUpdateIn positionUpdate)
        {
            PositionUpdateReceived?.Invoke(this, in positionUpdate);
            
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

        public bool GetPositionUpdateForCar(EntryCar toCar, out PositionUpdateOut positionUpdateOut)
        {
            CarStatus targetCarStatus;
            var toTargetCar = toCar.TargetCar;
            if (toTargetCar != null)
            {
                if (toTargetCar.AiControlled && toTargetCar.LastSeenAiState[toCar.SessionId] != null)
                {
                    targetCarStatus = toTargetCar.LastSeenAiState[toCar.SessionId]!.Status;
                }
                else
                {
                    targetCarStatus = toTargetCar.Status;
                }
            }
            else
            {
                targetCarStatus = toCar.Status;
            }

            CarStatus status;
            if (AiControlled)
            {
                var aiState = GetBestStateForPlayer(targetCarStatus);

                if (aiState == null)
                {
                    positionUpdateOut = default;
                    return false;
                }

                if (LastSeenAiState[toCar.SessionId] != aiState
                    || LastSeenAiSpawn[toCar.SessionId] != aiState.SpawnCounter)
                {
                    LastSeenAiState[toCar.SessionId] = aiState;
                    LastSeenAiSpawn[toCar.SessionId] = aiState.SpawnCounter;

                    if (AiEnableColorChanges)
                    {
                        toCar.Client?.SendPacket(new CSPCarColorUpdate
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
                    positionUpdateOut = default;
                    return false;
                }

                OtherCarsLastSentUpdateTime[toCar.SessionId] = Environment.TickCount64;
            }

            positionUpdateOut = new PositionUpdateOut(SessionId,
                AiControlled ? AiPakSequenceIds[toCar.SessionId]++ : status.PakSequenceId,
                (uint)(status.Timestamp - toCar.TimeOffset),
                Ping,
                status.Position,
                status.Rotation,
                status.Velocity,
                status.TyreAngularSpeed[0],
                status.TyreAngularSpeed[1],
                status.TyreAngularSpeed[2],
                status.TyreAngularSpeed[3],
                status.SteerAngle,
                status.WheelAngle,
                status.EngineRpm,
                status.Gear,
                (Server.Configuration.Extra.ForceLights || ForceLights)
                    ? status.StatusFlag | CarStatusFlags.LightsOn
                    : status.StatusFlag,
                status.PerformanceDelta,
                status.Gas);
            return true;
        }
    }
}
