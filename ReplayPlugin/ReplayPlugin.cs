using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using AssettoServer.Server;
using AssettoServer.Server.Ai;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Plugin;
using AssettoServer.Server.Weather;
using AssettoServer.Shared.Services;
using AssettoServer.Shared.Weather;
using AssettoServer.Utils;
using Microsoft.Extensions.Hosting;
using Prometheus;
using ReplayPlugin.Data;
using Serilog;
using SerilogTimings;

namespace ReplayPlugin;

public class ReplayPlugin : CriticalBackgroundService, IAssettoServerAutostart
{
    private readonly ACServerConfiguration _configuration;
    private readonly EntryCarManager _entryCarManager;
    private readonly SessionManager _session;
    private readonly WeatherManager _weather;
    private readonly Lazy<ACServer> _server;
    private readonly ReplayManager _replayManager;
    private readonly Summary _onUpdateTimer;

    private readonly List<ReplayFrame> _frames = [];
    
    public ReplayPlugin(IHostApplicationLifetime applicationLifetime, Lazy<ACServer> server, ACServerConfiguration configuration, EntryCarManager entryCarManager, WeatherManager weather, SessionManager session, ReplayManager replayManager) : base(applicationLifetime)
    {
        _server = server;
        _configuration = configuration;
        _entryCarManager = entryCarManager;
        _weather = weather;
        _session = session;
        _replayManager = replayManager;
        
        _onUpdateTimer = Metrics.CreateSummary("assettoserver_replayplugin_onupdate", "ReplayPlugin.OnUpdate Duration", MetricDefaults.DefaultQuantiles);
    }

    private void OnUpdate(ACServer sender, EventArgs args)
    {
        using var timer = _onUpdateTimer.NewTimer();
        
        var carFrames = new List<ReplayCarFrame>();
        var aiFrames = new List<ReplayCarFrame>();
        var aiStateMapping = new Dictionary<AiState, ushort>();
        var aiFrameMapping = new Dictionary<byte, List<ushort>>();
        foreach (var entryCar in _entryCarManager.EntryCars)
        {
            if (entryCar.Client?.HasSentFirstUpdate == true)
            {
                carFrames.Add(new ReplayCarFrame
                {
                    SessionId = entryCar.SessionId,
                    WorldTranslation = entryCar.Status.Position,
                    WorldOrientation = new HVector3(entryCar.Status.Rotation),
                    EngineRpm = (Half)entryCar.Status.EngineRpm,
                    Gas = entryCar.Status.Gas,
                    Gear = entryCar.Status.Gear,
                    Velocity = entryCar.Status.Velocity,
                    Connected = true
                });
                
                aiFrameMapping.Add(entryCar.SessionId, new List<ushort>());
            }
            else if (entryCar.AiControlled)
            {
                for (int i = 0; i < entryCar.LastSeenAiState.Length; i++)
                {
                    var aiState = entryCar.LastSeenAiState[i];
                    if (aiState == null) continue;
                    
                    if (!aiStateMapping.TryGetValue(aiState, out var aiStateId))
                    {
                        aiStateId = (ushort)aiFrames.Count;
                        aiStateMapping.Add(aiState, aiStateId);
                        aiFrames.Add(new ReplayCarFrame
                        {
                            SessionId = aiState.EntryCar.SessionId,
                            WorldTranslation = aiState.Status.Position,
                            WorldOrientation = new HVector3(aiState.Status.Rotation),
                            EngineRpm = (Half)aiState.Status.EngineRpm,
                            Gas = aiState.Status.Gas,
                            Gear = aiState.Status.Gear,
                            Velocity = aiState.Status.Velocity,
                            Connected = true
                        });
                    }

                    if (aiFrameMapping.TryGetValue((byte)i, out var aiFrameMappingList))
                    {
                        aiFrameMappingList.Add(aiStateId);
                    }
                }
            }
        }
        
        _frames.Add(new ReplayFrame
        {
            ServerTime = _session.ServerTimeMilliseconds,
            SunAngle = (Half)WeatherUtils.SunAngleFromSeconds(_weather.CurrentDateTime.TimeOfDay.TickOfDay / 10_000_000.0),
            CarFrames = carFrames.ToArray(),
            AiFrames = aiFrames.ToArray(),
            AiFrameMapping = aiFrameMapping
        });

        if (_frames.Count % 600 == 0)
        {
            Log.Debug("Writing replay...");
            using (var t = Operation.Time("Writing replay 0"))
            {
                _replayManager.WriteReplay(_frames, 0, "out_0.acreplay");
            }

            using (var t = Operation.Time("Writing replay 1"))
            {
                _replayManager.WriteReplay(_frames, 1, "out_1.acreplay");
            }
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _server.Value.Update += OnUpdate;

        return Task.CompletedTask;
    }
}
