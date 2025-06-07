using System.Numerics;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Server.Ai.Splines;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Weather;
using AssettoServer.Shared.Network.Packets.Incoming;
using AssettoServer.Shared.Network.Packets.Outgoing;
using AssettoServer.Shared.Network.Packets.Shared;
using AutoModerationPlugin.Packets;
using Serilog;

namespace AutoModerationPlugin;

public class EntryCarAutoModeration
{
    private int CurrentSplinePointId { get; set; } = -1;
    private float CurrentSplinePointDistanceSquared { get; set; }

    private bool HasSentAfkWarning { get; set; }
    private long LastMovementTime { get; set; }
    private long LastActiveTime => _configuration.AfkPenalty.Behavior == AfkPenaltyBehavior.PlayerInput
        ? _entryCar.LastActiveTime
        : LastMovementTime;
    
    private int HighPingSeconds { get; set; }
    private bool HasSentHighPingWarning { get; set; }

    private int NoLightSeconds { get; set; }
    private bool HasSentNoLightWarning { get; set; }
    private int NoLightsPitCount { get; set; }

    private int WrongWaySeconds { get; set; }
    private bool HasSentWrongWayWarning { get; set; }
    private int WrongWayPitCount { get; set; }

    private int BlockingRoadSeconds { get; set; }
    private bool HasSentBlockingRoadWarning { get; set; }
    private int BlockingRoadPitCount { get; set; }

    private Flags CurrentFlags { get; set; }

    private const double NauticalTwilight = -12.0 * Math.PI / 180.0;
    private readonly EntryCar _entryCar;
    private readonly AiSpline? _aiSpline;
    private readonly ACServerConfiguration _serverConfiguration;
    private readonly AutoModerationConfiguration _configuration;
    private readonly EntryCarManager _entryCarManager;
    private readonly WeatherManager _weatherManager;
    private readonly SessionManager _sessionManager;
    private readonly float _laneRadiusSquared;
    
    public EntryCarAutoModeration(EntryCar entryCar,
        AutoModerationConfiguration configuration,
        EntryCarManager entryCarManager,
        WeatherManager weatherManager,
        SessionManager sessionManager,
        ACServerConfiguration serverConfiguration,
        AiSpline? aiSpline = null)
    {
        _entryCar = entryCar;
        _configuration = configuration;
        _entryCarManager = entryCarManager;
        _weatherManager = weatherManager;
        _sessionManager = sessionManager;
        _serverConfiguration = serverConfiguration;
        _aiSpline = aiSpline;
        _entryCar.ResetInvoked += OnResetInvoked;
        if (_configuration.AfkPenalty is { Enabled: true, Behavior: AfkPenaltyBehavior.MinimumSpeed })
        {
            _entryCar.PositionUpdateReceived += OnPositionUpdateReceived;
        }
        
        _laneRadiusSquared = MathF.Pow(_serverConfiguration.Extra.AiParams.LaneWidthMeters / 2.0f * 1.25f, 2);
    }

    private void OnPositionUpdateReceived(EntryCar sender, in PositionUpdateIn positionUpdate)
    {
        const float afkMinSpeed = 20 / 3.6f;
        const float afkMinSpeedSquared = afkMinSpeed * afkMinSpeed;

        if (positionUpdate.Velocity.LengthSquared() > afkMinSpeedSquared)
        {
            SetActive();
        }
    }

    private void OnResetInvoked(EntryCar sender, EventArgs args)
    {
        HasSentAfkWarning = false;
        SetActive();
        
        HighPingSeconds = 0;
        HasSentHighPingWarning = false;
        
        NoLightSeconds = 0;
        NoLightsPitCount = 0;
        HasSentNoLightWarning = false;
        
        WrongWaySeconds = 0;
        WrongWayPitCount = 0;
        HasSentWrongWayWarning = false;
        
        BlockingRoadSeconds = 0;
        BlockingRoadPitCount = 0;
        HasSentBlockingRoadWarning = false;
        
        CurrentFlags = 0;
    }

    internal void AdminReset()
    {
        OnResetInvoked(_entryCar, EventArgs.Empty);
        if (_serverConfiguration.Extra.EnableClientMessages)
        {
            _entryCar.Client?.SendPacket(new AutoModerationFlags { Flags = 0 });
        }
    }

    public void Update()
    {
        var client = _entryCar.Client;
        if (client == null || !client.HasSentFirstUpdate || client.IsAdministrator)
            return;
        
        var oldFlags = CurrentFlags;
        
        UpdateSplinePoint();
        UpdateAfkPenalty(client);
        UpdateHighPingPenalty(client);
        UpdateNoLightsPenalty(client);
        UpdateWrongWayPenalty(client);
        UpdateBlockingRoadPenalty(client);
        
        if (_serverConfiguration.Extra.EnableClientMessages && oldFlags != CurrentFlags)
        {
            client.SendPacket(new AutoModerationFlags { Flags = CurrentFlags });
        }
    }

    private void UpdateAfkPenalty(ACTcpClient client)
    {
        if (!_configuration.AfkPenalty.Enabled) return;
        
        if (_configuration.AfkPenalty.IgnoreWithOpenSlots
            && _entryCarManager.EntryCars.Any(e => e.Model == _entryCar.Model && e.Client == null)) return;

        if (_configuration.AfkPenalty.ExcludedModels.Contains(client.EntryCar.Model)) return;

        var afkTime = _sessionManager.ServerTimeMilliseconds - LastActiveTime;
        if (afkTime > _configuration.AfkPenalty.DurationMilliseconds - 60_000)
        {
            if (!HasSentAfkWarning)
            {
                HasSentAfkWarning = true;
                client.SendChatMessage("You will be kicked in 1 minute for being AFK.");
            }
            else if (afkTime > _configuration.AfkPenalty.DurationMilliseconds)
            {
                _ = _entryCarManager.KickAsync(client, "being AFK");
            }
        }
        else
        {
            HasSentAfkWarning = false;
        }
    }

    private void UpdateHighPingPenalty(ACTcpClient client)
    {
        if (!_configuration.HighPingPenalty.Enabled) return;
        
        if (_entryCar.Ping > _configuration.HighPingPenalty.MaximumPingMilliseconds)
        {
            HighPingSeconds++;
                            
            if (HighPingSeconds > _configuration.HighPingPenalty.DurationSeconds)
            {
                _ = _entryCarManager.KickAsync(client, "high ping");
            }
            else if (!HasSentHighPingWarning && HighPingSeconds > _configuration.HighPingPenalty.DurationSeconds / 2)
            {
                HasSentHighPingWarning = true;
                client.SendChatMessage("You have a high ping, please fix your network connection or you will be kicked.");
            }
        }
        else
        {
            HighPingSeconds = 0;
            HasSentHighPingWarning = false;
        }
    }

    private void UpdateNoLightsPenalty(ACTcpClient client)
    {
        if (!_configuration.NoLightsPenalty.Enabled) return;
        
        if (_weatherManager.CurrentSunPosition!.Value.Altitude < NauticalTwilight
            && (_entryCar.Status.StatusFlag & CarStatusFlags.LightsOn) == 0
            && _entryCar.Status.Velocity.LengthSquared() > _configuration.NoLightsPenalty.MinimumSpeedMs * _configuration.NoLightsPenalty.MinimumSpeedMs)
        {
            // Would be nice if no flag was shown when just flashing the lights
            if (NoLightSeconds > _configuration.NoLightsPenalty.IgnoreSeconds)
            {
                CurrentFlags |= Flags.NoLights;
            }
            NoLightSeconds++;
            
            if (NoLightSeconds > _configuration.NoLightsPenalty.DurationSeconds)
            {
                if (NoLightsPitCount < _configuration.NoLightsPenalty.PitsBeforeKick)
                {
                    TeleportToPits(client, "driving without lights");
                    NoLightsPitCount++;
                }
                else
                {
                    _ = _entryCarManager.KickAsync(client, "driving without lights");
                }
            }
            else if (!HasSentNoLightWarning && NoLightSeconds > _configuration.NoLightsPenalty.DurationSeconds / 2)
            {
                HasSentNoLightWarning = true;
                var message = NoLightsPitCount < _configuration.NoLightsPenalty.PitsBeforeKick
                    ? "It is currently night, please turn on your lights or you will be teleported to pits."
                    : "It is currently night, please turn on your lights or you will be kicked.";
                client.SendChatMessage(message);
            }
        }
        else
        {
            CurrentFlags &= ~Flags.NoLights;
            NoLightSeconds = 0;
            HasSentNoLightWarning = false;
        }
    }

    private void UpdateWrongWayPenalty(ACTcpClient client)
    {
        if (!_configuration.WrongWayPenalty.Enabled || _aiSpline == null) return;
        
        if (CurrentSplinePointId >= 0
            && CurrentSplinePointDistanceSquared < _laneRadiusSquared
            && _entryCar.Status.Velocity.LengthSquared() > _configuration.WrongWayPenalty.MinimumSpeedMs * _configuration.WrongWayPenalty.MinimumSpeedMs
            && Vector3.Dot(_aiSpline.Operations.GetForwardVector(CurrentSplinePointId), _entryCar.Status.Velocity) < 0)
        {
            CurrentFlags |= Flags.WrongWay;
            
            WrongWaySeconds++;
            if (WrongWaySeconds > _configuration.WrongWayPenalty.DurationSeconds)
            {
                if (WrongWayPitCount < _configuration.WrongWayPenalty.PitsBeforeKick)
                {
                    TeleportToPits(client, "driving the wrong way");
                    WrongWayPitCount++;
                }
                else
                {
                    _ = _entryCarManager.KickAsync(client, "driving the wrong way");
                }
            }
            else if (!HasSentWrongWayWarning && WrongWaySeconds > _configuration.WrongWayPenalty.DurationSeconds / 2)
            {
                HasSentWrongWayWarning = true;
                var message = WrongWayPitCount < _configuration.WrongWayPenalty.PitsBeforeKick
                    ? "You are driving the wrong way! Turn around or you will be teleported to pits."
                    : "You are driving the wrong way! Turn around or you will be kicked.";
                client.SendChatMessage(message);
            }
        }
        else
        {
            CurrentFlags &= ~Flags.WrongWay;
            WrongWaySeconds = 0;
            HasSentWrongWayWarning = false;
        }
    }

    private void UpdateBlockingRoadPenalty(ACTcpClient client)
    {
        if (!_configuration.BlockingRoadPenalty.Enabled) return;
        
        if (CurrentSplinePointDistanceSquared < _laneRadiusSquared
            && _entryCar.Status.Velocity.LengthSquared() < _configuration.BlockingRoadPenalty.MaximumSpeedMs * _configuration.BlockingRoadPenalty.MaximumSpeedMs)
        {
            CurrentFlags |= Flags.NoParking;
            
            BlockingRoadSeconds++;
            if (BlockingRoadSeconds > _configuration.BlockingRoadPenalty.DurationSeconds)
            {
                if (BlockingRoadPitCount < _configuration.BlockingRoadPenalty.PitsBeforeKick)
                {
                    TeleportToPits(client, "blocking the road");
                    BlockingRoadPitCount++;
                }
                else
                {
                    _ = _entryCarManager.KickAsync(client, "blocking the road");
                }
            }
            else if (!HasSentBlockingRoadWarning && BlockingRoadSeconds > _configuration.BlockingRoadPenalty.DurationSeconds / 2)
            {
                HasSentBlockingRoadWarning = true;
                var message = BlockingRoadPitCount < _configuration.BlockingRoadPenalty.PitsBeforeKick
                    ? "You are blocking the road! Please move or you will be teleported to pits."
                    : "You are blocking the road! Please move or teleport to pits, or you will be kicked.";
                client.SendChatMessage(message);
            }
        }
        else
        {
            CurrentFlags &= ~Flags.NoParking;
            BlockingRoadSeconds = 0;
            HasSentBlockingRoadWarning = false;
        }
    }

    public void SetActive()
    {
        LastMovementTime = _sessionManager.ServerTimeMilliseconds;
    }

    private void UpdateSplinePoint()
    {
        if (_aiSpline != null)
        {
            (CurrentSplinePointId, CurrentSplinePointDistanceSquared) = _aiSpline.WorldToSpline(_entryCar.Status.Position);
        }
    }
    
    private void TeleportToPits(ACTcpClient player, string reason)
    {
        _sessionManager.SendCurrentSession(player);
        player.SendChatMessage($"You have been teleported to the pits for {reason}.");
    }
}
