using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using AssettoServer.Server;
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
    
    private readonly ReplaySegment _segment = new();
    
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

    private readonly ReplayFrameState _state = new();

    private void OnUpdate(ACServer sender, EventArgs args)
    {
        using var timer = _onUpdateTimer.NewTimer();
        
        _state.TryReset();

        int numAiMappings = 0;
        foreach (var entryCar in _entryCarManager.EntryCars)
        {
            if (entryCar.Client?.HasSentFirstUpdate == true)
            {
                _state.PlayerCars.Add((entryCar.SessionId, entryCar.Status));
                _state.AiFrameMapping.Add(entryCar.SessionId, new List<short>());
                numAiMappings++;
            }
            else if (entryCar.AiControlled)
            {
                for (int i = 0; i < entryCar.LastSeenAiState.Length; i++)
                {
                    var aiState = entryCar.LastSeenAiState[i];
                    if (aiState == null) continue;
                    
                    if (!_state.AiStateMapping.TryGetValue(aiState, out var aiStateId))
                    {
                        aiStateId = (short)_state.AiCars.Count;
                        _state.AiStateMapping.Add(aiState, aiStateId);
                        _state.AiCars.Add((aiState.EntryCar.SessionId, aiState.Status));
                    }

                    if (_state.AiFrameMapping.TryGetValue((byte)i, out var aiFrameMappingList))
                    {
                        aiFrameMappingList.Add(aiStateId);
                        numAiMappings++;
                    }
                }
            }
        }

        if (_segment.TryAddFrame(_state.PlayerCars.Count, _state.AiCars.Count, numAiMappings, this, WriteFrame))
        {
            
        }
        else
        {
            Log.Debug("Segment full");
            _server.Value.Update -= OnUpdate;
        }

        if (_segment.Index.Count % 600 == 0)
        {
            Log.Debug("Writing replay...");
            using (var t = Operation.Time("Writing replay 0"))
            {
                _replayManager.WriteReplay(_segment, 0, "out_0.acreplay");
            }

            using (var t = Operation.Time("Writing replay 1"))
            {
                _replayManager.WriteReplay(_segment, 1, "out_1.acreplay");
            }
        }
    }

    private static void WriteFrame(ref ReplayFrame frame, ReplayPlugin self)
    {
        frame.Header.ServerTime = self._session.ServerTimeMilliseconds;
        frame.Header.SunAngle = (Half)WeatherUtils.SunAngleFromSeconds(self._weather.CurrentDateTime.TimeOfDay.TickOfDay / 10_000_000.0);

        short aiFrameMappingIndex = 0;
        for (int i = 0; i < self._state.PlayerCars.Count; i++)
        {
            var status = self._state.PlayerCars[i];
            frame.CarFrames[i] = ReplayCarFrame.FromCarStatus(status.Item1, status.Item2);

            if (self._state.AiFrameMapping.TryGetValue(status.Item1, out var aiFrameMapping))
            {
                frame.CarFrames[i].AiMappingStartIndex = aiFrameMappingIndex;
                frame.AiMappings[aiFrameMappingIndex++] = (short)aiFrameMapping.Count;
                aiFrameMapping.CopyTo(frame.AiMappings[aiFrameMappingIndex..]);

                aiFrameMappingIndex += (short)aiFrameMapping.Count;
            }
        }

        for (int i = 0; i < self._state.AiCars.Count; i++)
        {
            var status = self._state.AiCars[i];
            frame.AiFrames[i] = ReplayCarFrame.FromCarStatus(status.Item1, status.Item2);
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _server.Value.Update += OnUpdate;
        
        Log.Debug("sizeof {0} mm {1}", Unsafe.SizeOf<ReplayCarFrame>(), Marshal.SizeOf<ReplayCarFrame>());
        return Task.CompletedTask;
    }
}
