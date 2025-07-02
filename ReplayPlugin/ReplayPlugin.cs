using System.Reflection;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Server.Weather;
using AssettoServer.Shared.Weather;
using AssettoServer.Utils;
using Microsoft.Extensions.Hosting;
using Prometheus;
using ReplayPlugin.Data;
using ReplayPlugin.Packets;
using Serilog;

namespace ReplayPlugin;

public class ReplayPlugin : IHostedService
{
    private readonly ReplayConfiguration _configuration;
    private readonly EntryCarManager _entryCarManager;
    private readonly SessionManager _session;
    private readonly WeatherManager _weather;
    private readonly ReplaySegmentManager _replaySegmentManager;
    private readonly Summary _onUpdateTimer;
    private readonly EntryCarExtraDataManager _extraData;
    private readonly ReplayMetadataProvider _metadata;
    
    public ReplayPlugin(EntryCarManager entryCarManager,
        WeatherManager weather,
        SessionManager session,
        ReplaySegmentManager replaySegmentManager,
        ReplayConfiguration configuration,
        CSPServerScriptProvider scriptProvider,
        CSPClientMessageTypeManager cspClientMessageTypeManager,
        EntryCarExtraDataManager extraData,
        ReplayMetadataProvider metadata,
        ACServer server)
    {
        _entryCarManager = entryCarManager;
        _weather = weather;
        _session = session;
        _replaySegmentManager = replaySegmentManager;
        _configuration = configuration;
        _extraData = extraData;
        _metadata = metadata;

        _onUpdateTimer = Metrics.CreateSummary("assettoserver_replayplugin_onupdate", "ReplayPlugin.OnUpdate Duration", MetricDefaults.DefaultQuantiles);
        server.Update += Update;
        
        cspClientMessageTypeManager.RegisterOnlineEvent<UploadDataPacket>(OnUploadData);
        scriptProvider.AddScript(Assembly.GetExecutingAssembly().GetManifestResourceStream("ReplayPlugin.lua.replay.lua")!, "replay.lua");
    }

    private void OnUploadData(ACTcpClient sender, UploadDataPacket packet)
    {
        var data = _extraData.Data[packet.CarId];
        data.WheelPositions = packet.WheelPositions;
    }

    private readonly ReplayFrameState _state = new();
    private long _counter;

    private void Update(ACServer sender, EventArgs args)
    {
        if (_counter++ % _configuration.RefreshRateDivisor != 0) return;

        try
        {
            using var timer = _onUpdateTimer.NewTimer();
            _state.Reset();

            int numAiMappings = 0;
            foreach (var entryCar in _entryCarManager.EntryCars)
            {
                if (entryCar.Client?.HasSentFirstUpdate == true)
                {
                    _state.PlayerCars.Add((entryCar.SessionId, entryCar.Status));
                    _state.AiFrameMapping.Add(entryCar.SessionId, []);
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

            _replaySegmentManager.AddFrame(_state.PlayerCars.Count, _state.AiCars.Count, numAiMappings, _metadata.Index, this, WriteFrame);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during replay update");
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

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _extraData.Initialize(_entryCarManager.EntryCars.Length);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
