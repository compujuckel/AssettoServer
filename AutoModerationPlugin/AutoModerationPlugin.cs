using System.Numerics;
using System.Reflection;
using AssettoServer.Network.Packets.Outgoing;
using AssettoServer.Network.Packets.Shared;
using AssettoServer.Server;
using AssettoServer.Server.Ai;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Plugin;
using AssettoServer.Server.Weather;
using AutoModerationPlugin.Packets;
using JetBrains.Annotations;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace AutoModerationPlugin;

[UsedImplicitly]
public class AutoModerationPlugin : BackgroundService, IAssettoServerAutostart
{
    private const double NauticalTwilight = -12.0 * Math.PI / 180.0;
    
    private readonly List<EntryCarAutoModeration> _instances = new();

    private readonly ACServerConfiguration _serverConfiguration;
    private readonly AutoModerationConfiguration _configuration;
    private readonly EntryCarManager _entryCarManager;
    private readonly WeatherManager _weatherManager;
    private readonly Func<EntryCar, EntryCarAutoModeration> _entryCarAutoModerationFactory;

    private readonly float _laneRadiusSquared;

    public AutoModerationPlugin(AutoModerationConfiguration configuration, 
        EntryCarManager entryCarManager,
        WeatherManager weatherManager,
        ACServerConfiguration serverConfiguration,
        CSPServerScriptProvider scriptProvider,
        Func<EntryCar, EntryCarAutoModeration> entryCarAutoModerationFactory,
        TrafficMap? trafficMap = null)
    {
        _configuration = configuration;
        _entryCarManager = entryCarManager;
        _weatherManager = weatherManager;
        _serverConfiguration = serverConfiguration;
        _entryCarAutoModerationFactory = entryCarAutoModerationFactory;

        if (trafficMap == null)
        {
            if (_configuration.WrongWayKick.Enabled)
            {
                throw new ConfigurationException("AutoModerationPlugin: Wrong way kick does not work with AI traffic disabled");
            }

            if (_configuration.BlockingRoadKick.Enabled)
            {
                throw new ConfigurationException("AutoModerationPlugin: Blocking road kick does not work with AI traffic disabled");
            }
        }
        else 
        {
            _laneRadiusSquared = MathF.Pow(_serverConfiguration.Extra.AiParams.LaneWidthMeters / 2.0f * 1.25f, 2);
        }
        
        if (_serverConfiguration.Extra.EnableClientMessages)
        {
            using var streamReader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("AutoModerationPlugin.lua.automoderation.lua")!);
            scriptProvider.AddScript(streamReader.ReadToEnd(), "automoderation.lua");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        foreach (var entryCar in _entryCarManager.EntryCars)
        {
            _instances.Add(_entryCarAutoModerationFactory(entryCar));
        }
        
        if (_configuration.NoLightsKick.Enabled && !_weatherManager.CurrentSunPosition.HasValue)
        {
            throw new ConfigurationException("AutoModerationPlugin: No lights kick does not work with missing track params");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                foreach (var instance in _instances)
                {
                    var client = instance.EntryCar.Client;
                    if (client == null || !client.HasSentFirstUpdate || client.IsAdministrator)
                        continue;

                    var oldFlags = instance.CurrentFlags;
                    instance.UpdateSplinePoint();

                    if (_configuration.NoLightsKick.Enabled)
                    {
                        if (_weatherManager.CurrentSunPosition!.Value.Altitude < NauticalTwilight
                            && (instance.EntryCar.Status.StatusFlag & CarStatusFlags.LightsOn) == 0
                            && instance.EntryCar.Status.Velocity.LengthSquared() > _configuration.NoLightsKick.MinimumSpeedMs * _configuration.NoLightsKick.MinimumSpeedMs)
                        {
                            instance.CurrentFlags |= Flags.NoLights;
                            
                            instance.NoLightSeconds++;
                            if (instance.NoLightSeconds > _configuration.NoLightsKick.DurationSeconds)
                            {
                                _ = _entryCarManager.KickAsync(client, "driving without lights");
                            }
                            else if (!instance.HasSentNoLightWarning && instance.NoLightSeconds > _configuration.NoLightsKick.DurationSeconds / 2)
                            {
                                instance.HasSentNoLightWarning = true;
                                client.SendPacket(new ChatMessage { SessionId = 255, Message = "It is currently night, please turn on your lights or you will be kicked." });
                            }
                        }
                        else
                        {
                            instance.CurrentFlags &= ~Flags.NoLights;
                            instance.NoLightSeconds = 0;
                            instance.HasSentNoLightWarning = false;
                        }
                    }

                    if (_configuration.WrongWayKick.Enabled)
                    {
                        if (instance.CurrentSplinePointDistanceSquared < _laneRadiusSquared
                            && instance.EntryCar.Status.Velocity.LengthSquared() > _configuration.WrongWayKick.MinimumSpeedMs * _configuration.WrongWayKick.MinimumSpeedMs
                            && Vector3.Dot(instance.CurrentSplinePoint!.GetForwardVector(), instance.EntryCar.Status.Velocity) < 0)
                        {
                            instance.CurrentFlags |= Flags.WrongWay;
                            
                            instance.WrongWaySeconds++;
                            if (instance.WrongWaySeconds > _configuration.WrongWayKick.DurationSeconds)
                            {
                                _ = _entryCarManager.KickAsync(client, "driving the wrong way");
                            }
                            else if (!instance.HasSentWrongWayWarning && instance.WrongWaySeconds > _configuration.WrongWayKick.DurationSeconds / 2)
                            {
                                instance.HasSentWrongWayWarning = true;
                                client.SendPacket(new ChatMessage { SessionId = 255, Message = "You are driving the wrong way! Turn around or you will be kicked." });
                            }
                        }
                        else
                        {
                            instance.CurrentFlags &= ~Flags.WrongWay;
                            instance.WrongWaySeconds = 0;
                            instance.HasSentWrongWayWarning = false;
                        }
                    }

                    if (_configuration.BlockingRoadKick.Enabled)
                    {
                        if (instance.CurrentSplinePointDistanceSquared < _laneRadiusSquared
                            && instance.EntryCar.Status.Velocity.LengthSquared() < _configuration.BlockingRoadKick.MaximumSpeedMs * _configuration.BlockingRoadKick.MaximumSpeedMs)
                        {
                            instance.CurrentFlags |= Flags.NoParking;
                            
                            instance.BlockingRoadSeconds++;
                            if (instance.BlockingRoadSeconds > _configuration.BlockingRoadKick.DurationSeconds)
                            {
                                _ = _entryCarManager.KickAsync(client, "blocking the road");
                            }
                            else if (!instance.HasSentBlockingRoadWarning && instance.BlockingRoadSeconds > _configuration.BlockingRoadKick.DurationSeconds / 2)
                            {
                                instance.HasSentBlockingRoadWarning = true;
                                client.SendPacket(new ChatMessage { SessionId = 255, Message = "You are blocking the road! Please move or teleport to pits, or you will be kicked." });
                            }
                        }
                        else
                        {
                            instance.CurrentFlags &= ~Flags.NoParking;
                            instance.BlockingRoadSeconds = 0;
                            instance.HasSentBlockingRoadWarning = false;
                        }
                    }

                    if (_serverConfiguration.Extra.EnableClientMessages && oldFlags != instance.CurrentFlags)
                    {
                        client.SendPacket(new AutoModerationFlags
                        {
                            Flags = instance.CurrentFlags
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during auto moderation update");
            }
            finally
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}
