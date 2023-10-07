﻿using AssettoServer.Network.Tcp;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using AssettoServer.Server.Ai;
using AssettoServer.Server.Ai.Splines;
using AssettoServer.Server.Configuration;
using AssettoServer.Shared.Model;
using AssettoServer.Shared.Network.Packets.Incoming;
using AssettoServer.Shared.Network.Packets.Outgoing;
using AssettoServer.Shared.Network.Packets.Shared;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace AssettoServer.Server;

public partial class EntryCar : IEntryCar<ACTcpClient>
{ 
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
    public long LastPingTime { get; internal set; }
    public long LastPongTime { get; internal set; }
    public ushort Ping { get; internal set; }
    public DriverOptionsFlags DriverOptionsFlags { get; internal set; }

    public bool IsSpectator { get; internal set; }
    public string Model { get; }
    public string Skin { get; }
    public int SpectatorMode { get; internal set; }
    public int Ballast { get; internal set; }
    public int Restrictor { get; internal set; }
    public List<ulong> AllowedGuids { get; internal set; } = new();
        
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

    public delegate EntryCar Factory(string model, string? skin, byte sessionId);

    private readonly ACServerConfiguration _configuration;
    private readonly EntryCarManager _entryCarManager;
    private readonly SessionManager _sessionManager;

    public ILogger Logger { get; }

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
        
    public EntryCar(string model, string? skin, byte sessionId, Func<EntryCar, AiState> aiStateFactory, SessionManager sessionManager, ACServerConfiguration configuration, EntryCarManager entryCarManager, AiSpline? spline = null)
    {
        Model = model;
        Skin = skin ?? "";
        SessionId = sessionId;
        _sessionManager = sessionManager;
        _configuration = configuration;
        _entryCarManager = entryCarManager;
        _spline = spline;
        _aiStateFactory = aiStateFactory;
        OtherCarsLastSentUpdateTime = new long[entryCarManager.EntryCars.Length];

        AiPakSequenceIds = new byte[entryCarManager.EntryCars.Length];
        LastSeenAiState = new AiState[entryCarManager.EntryCars.Length];
        LastSeenAiSpawn = new byte[entryCarManager.EntryCars.Length];
            
        Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.With(new EntryCarLogEventEnricher(this))
            .WriteTo.Logger(Log.Logger)
            .CreateLogger();
            
        AiInit();
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
        if (!_configuration.Extra.EnableAntiAfk || Client?.IsAdministrator == true || !Client?.HasSentFirstUpdate == true)
            return;

        long timeAfk = _sessionManager.ServerTimeMilliseconds - LastActiveTime;
        if (timeAfk > _configuration.Extra.MaxAfkTimeMilliseconds)
            _ = Task.Run(() => _entryCarManager.KickAsync(Client,  "being AFK"));
        else if (!HasSentAfkWarning && _configuration.Extra.MaxAfkTimeMilliseconds - timeAfk < 60000)
        {
            HasSentAfkWarning = true;
            Client?.SendPacket(new ChatMessage { SessionId = 255, Message = "You will be kicked in 1 minute for being AFK." });
        }
    }

    internal void SetActive()
    {
        LastActiveTime = _sessionManager.ServerTimeMilliseconds;
        HasSentAfkWarning = false;
    }

    internal void UpdatePosition(in PositionUpdateIn positionUpdate)
    {
        if (!positionUpdate.IsValid())
        {
            var client = Client;
            if (client == null) return;
            client.Logger.Debug("Invalid position update received from {ClientName} ({SessionId}), disconnecting", client.Name, client.SessionId);
            _ = client.DisconnectAsync();
            return;
        }

        PositionUpdateReceived?.Invoke(this, in positionUpdate);
            
        HasUpdateToSend = true;
        LastRemoteTimestamp = positionUpdate.LastRemoteTimestamp;

        const float afkMinSpeed = 20 / 3.6f;
        if ((positionUpdate.StatusFlag != Status.StatusFlag || positionUpdate.Gas != Status.Gas || positionUpdate.SteerAngle != Status.SteerAngle)
            && (_configuration.Extra.AfkKickBehavior != AfkKickBehavior.MinimumSpeed || positionUpdate.Velocity.LengthSquared() > afkMinSpeed * afkMinSpeed))
        {
            SetActive();
        }
            
        if (Status.Velocity.Y < -75 && _sessionManager.ServerTimeMilliseconds - LastFallCheckTime > 1000)
        {
            LastFallCheckTime = _sessionManager.ServerTimeMilliseconds;
            if(Client != null)
                _sessionManager.SendCurrentSession(Client);
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
            if ((_sessionManager.ServerTimeMilliseconds - OtherCarsLastSentUpdateTime[toCar.SessionId]) < OutsideNetworkBubbleUpdateRateMs)
            {
                positionUpdateOut = default;
                return false;
            }

            OtherCarsLastSentUpdateTime[toCar.SessionId] = _sessionManager.ServerTimeMilliseconds;
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
            (_configuration.Extra.ForceLights || ForceLights)
                ? status.StatusFlag | CarStatusFlags.LightsOn
                : status.StatusFlag,
            status.PerformanceDelta,
            status.Gas);
        return true;
    }
    
    public bool IsInRange(EntryCar target, float range)
    {
        var targetPosition = target.TargetCar != null ? target.TargetCar.Status.Position : target.Status.Position;
        return Vector3.DistanceSquared(Status.Position, targetPosition) < range * range;
    }
}
