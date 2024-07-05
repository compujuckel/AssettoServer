using System.Reflection;
using System.Runtime.InteropServices;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Server.Plugin;
using AssettoServer.Server.Weather;
using AssettoServer.Shared.Services;
using AssettoServer.Shared.Weather;
using AssettoServer.Utils;
using Microsoft.Extensions.Hosting;
using Prometheus;
using ReplayPlugin.Data;
using ReplayPlugin.Packets;
using Serilog;

namespace ReplayPlugin;

public class ReplayPlugin : CriticalBackgroundService, IAssettoServerAutostart
{
    private readonly ReplayConfiguration _configuration;
    private readonly EntryCarManager _entryCarManager;
    private readonly SessionManager _session;
    private readonly WeatherManager _weather;
    private readonly ReplayManager _replayManager;
    private readonly Summary _onUpdateTimer;
    private readonly EntryCarExtraDataManager _extraData;
    
    public ReplayPlugin(IHostApplicationLifetime applicationLifetime,
        EntryCarManager entryCarManager,
        WeatherManager weather,
        SessionManager session,
        ReplayManager replayManager,
        ReplayConfiguration configuration,
        CSPServerScriptProvider scriptProvider,
        CSPClientMessageTypeManager cspClientMessageTypeManager,
        EntryCarExtraDataManager extraData) : base(applicationLifetime)
    {
        _entryCarManager = entryCarManager;
        _weather = weather;
        _session = session;
        _replayManager = replayManager;
        _configuration = configuration;
        _extraData = extraData;

        _onUpdateTimer = Metrics.CreateSummary("assettoserver_replayplugin_onupdate", "ReplayPlugin.OnUpdate Duration", MetricDefaults.DefaultQuantiles);
        
        cspClientMessageTypeManager.RegisterOnlineEvent<UploadDataPacket>(OnUploadData);
        
        using var streamReader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("ReplayPlugin.lua.replay.lua")!);
        scriptProvider.AddScript(streamReader.ReadToEnd(), "replay.lua");
    }

    private void OnUploadData(ACTcpClient sender, UploadDataPacket packet)
    {
        var data = _extraData.Data[packet.CarId];
        data.WheelPositions = packet.WheelPositions;
    }

    private readonly ReplayFrameState _state = new();

    private void Update()
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

        _replayManager.AddFrame(_state.PlayerCars.Count, _state.AiCars.Count, numAiMappings, this, WriteFrame);
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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Debug("ReplayCarFrame size {Size} bytes", Marshal.SizeOf<ReplayCarFrame>());

        _extraData.Initialize(_entryCarManager.EntryCars.Length);
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(1000.0 / _configuration.RefreshRateHz));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                Update();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during replay update");
            }
        }
    }
}
