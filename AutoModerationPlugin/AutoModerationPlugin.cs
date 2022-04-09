using System.Numerics;
using AssettoServer.Network.Packets.Outgoing;
using AssettoServer.Network.Packets.Shared;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Plugin;
using JetBrains.Annotations;
using Serilog;

namespace AutoModerationPlugin;

[UsedImplicitly]
public class AutoModerationPlugin : IAssettoServerPlugin<AutoModerationConfiguration>
{
    private const double NauticalTwilight = -12.0 * Math.PI / 180.0;
    
    private readonly List<EntryCarAutoModeration> _instances = new();

    private AutoModerationConfiguration _configuration = new();
    private ACServer _server = null!;

    private float _laneRadiusSquared;

    public void SetConfiguration(AutoModerationConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void Initialize(ACServer server)
    {
        _server = server;

        if (_server.TrafficMap != null)
        {
            _laneRadiusSquared = MathF.Pow(_server.Configuration.Extra.AiParams.LaneWidthMeters / 2.0f * 1.25f, 2);
        }
        
        foreach (var entryCar in server.EntryCars)
        {
            _instances.Add(new EntryCarAutoModeration(entryCar));
        }

        _ = UpdateLoopAsync();
    }
    
    private async Task UpdateLoopAsync()
    {
        if (_configuration.NoLightsKick.Enabled && !_server.CurrentSunPosition.HasValue)
        {
            throw new ConfigurationException("AutoModerationPlugin: No lights kick does not work with missing track params");
        }

        if (_configuration.WrongWayKick.Enabled && _server.TrafficMap == null)
        {
            throw new ConfigurationException("AutoModerationPlugin: Wrong way kick does not work with AI traffic disabled");
        }
        
        if (_configuration.BlockingRoadKick.Enabled && _server.TrafficMap == null)
        {
            throw new ConfigurationException("AutoModerationPlugin: Blocking road kick does not work with AI traffic disabled");
        }

        while (true)
        {
            try
            {
                foreach (var instance in _instances)
                {
                    var client = instance.EntryCar.Client;
                    if (client == null || !client.HasSentFirstUpdate || client.IsAdministrator)
                        continue;

                    if (_configuration.NoLightsKick.Enabled)
                    {
                        if (_server.CurrentSunPosition!.Value.Altitude < NauticalTwilight
                            && (instance.EntryCar.Status.StatusFlag & CarStatusFlags.LightsOn) == 0
                            && instance.EntryCar.Status.Velocity.LengthSquared() > _configuration.NoLightsKick.MinimumSpeedMs * _configuration.NoLightsKick.MinimumSpeedMs)
                        {
                            instance.NoLightSeconds++;
                            if (instance.NoLightSeconds > _configuration.NoLightsKick.DurationSeconds)
                            {
                                _ = _server.KickAsync(client, KickReason.Kicked, $"{client.Name} has been kicked for driving without lights.");
                            }
                            else if (!instance.HasSentNoLightWarning && instance.NoLightSeconds > _configuration.NoLightsKick.DurationSeconds / 2)
                            {
                                instance.HasSentNoLightWarning = true;
                                client.SendPacket(new ChatMessage { SessionId = 255, Message = "It is currently night, please turn on your lights or you will be kicked." });
                            }
                        }
                        else
                        {
                            instance.NoLightSeconds = 0;
                            instance.HasSentNoLightWarning = false;
                        }
                    }

                    if (_configuration.WrongWayKick.Enabled)
                    {
                        if (instance.EntryCar.CurrentSplinePointDistanceSquared < _laneRadiusSquared
                            && instance.EntryCar.Status.Velocity.LengthSquared() > _configuration.WrongWayKick.MinimumSpeedMs * _configuration.WrongWayKick.MinimumSpeedMs
                            && Vector3.Dot(instance.EntryCar.CurrentSplinePoint!.GetForwardVector(), instance.EntryCar.Status.Velocity) < 0)
                        {
                            instance.WrongWaySeconds++;
                            if (instance.WrongWaySeconds > _configuration.WrongWayKick.DurationSeconds)
                            {
                                _ = _server.KickAsync(client, KickReason.Kicked, $"{client.Name} has been kicked for driving the wrong way.");
                            }
                            else if (!instance.HasSentWrongWayWarning && instance.WrongWaySeconds > _configuration.WrongWayKick.DurationSeconds / 2)
                            {
                                instance.HasSentWrongWayWarning = true;
                                client.SendPacket(new ChatMessage { SessionId = 255, Message = "You are driving the wrong way! Turn around or you will be kicked." });
                            }
                        }
                        else
                        {
                            instance.WrongWaySeconds = 0;
                            instance.HasSentWrongWayWarning = false;
                        }
                    }

                    if (_configuration.BlockingRoadKick.Enabled)
                    {
                        if (instance.EntryCar.CurrentSplinePointDistanceSquared < _laneRadiusSquared
                            && instance.EntryCar.Status.Velocity.LengthSquared() < _configuration.BlockingRoadKick.MaximumSpeedMs * _configuration.BlockingRoadKick.MaximumSpeedMs)
                        {
                            instance.BlockingRoadSeconds++;
                            if (instance.BlockingRoadSeconds > _configuration.BlockingRoadKick.DurationSeconds)
                            {
                                _ = _server.KickAsync(client, KickReason.Kicked, $"{client.Name} has been kicked for blocking the road.");
                            }
                            else if (!instance.HasSentBlockingRoadWarning && instance.BlockingRoadSeconds > _configuration.BlockingRoadKick.DurationSeconds / 2)
                            {
                                instance.HasSentBlockingRoadWarning = true;
                                client.SendPacket(new ChatMessage { SessionId = 255, Message = "You are blocking the road! Please move or teleport to pits, or you will be kicked." });
                            }
                        }
                        else
                        {
                            instance.BlockingRoadSeconds = 0;
                            instance.HasSentBlockingRoadWarning = false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during auto moderation update");
            }
            finally
            {
                await Task.Delay(1000);
            }
        }
    }
}