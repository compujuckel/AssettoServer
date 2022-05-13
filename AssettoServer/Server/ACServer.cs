using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using App.Metrics;
using App.Metrics.Counter;
using App.Metrics.Timer;
using AssettoServer.Network.Tcp;
using AssettoServer.Server.Configuration;
using AssettoServer.Network.Packets.Shared;
using AssettoServer.Network.Packets.Outgoing;
using AssettoServer.Network.Udp;
using AssettoServer.Server.Blacklist;
using AssettoServer.Server.GeoParams;
using AssettoServer.Server.Plugin;
using AssettoServer.Server.Weather;
using AssettoServer.Utils;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace AssettoServer.Server
{
    public class ACServer : BackgroundService
    {
        private readonly ACServerConfiguration _configuration;
        private readonly SessionManager _sessionManager;
        private readonly EntryCarManager _entryCarManager;
        private readonly WeatherManager _weatherManager;
        private readonly GeoParamsManager _geoParamsManager;
        private readonly IMetricsRoot _metrics;
        private readonly ChecksumManager _checksumManager;
        private readonly ACTcpServer _tcpServer;
        private readonly ACUdpServer _udpServer;
        private readonly KunosLobbyRegistration _kunosLobbyRegistration;
        private readonly IEnumerable<IAssettoServerAutostart> _autostartServices;
        private readonly IHostApplicationLifetime _applicationLifetime;

        /// <summary>
        /// Fires on each server tick in the main loop. Don't do resource intensive / long running stuff in here!
        /// </summary>
        public event EventHandler<ACServer, EventArgs>? Update;

        public ACServer(
            ACServerConfiguration configuration,
            IBlacklistService blacklistService,
            SessionManager sessionManager,
            EntryCarManager entryCarManager,
            WeatherManager weatherManager,
            GeoParamsManager geoParamsManager,
            IMetricsRoot metrics,
            ChecksumManager checksumManager,
            ACTcpServer tcpServer,
            ACUdpServer udpServer,
            CSPFeatureManager cspFeatureManager,
            IEnumerable<IAssettoServerAutostart> autostartServices,
            KunosLobbyRegistration kunosLobbyRegistration,
            IHostApplicationLifetime applicationLifetime)
        {
            Log.Information("Starting server");
            
            _configuration = configuration;
            _sessionManager = sessionManager;
            _entryCarManager = entryCarManager;
            _weatherManager = weatherManager;
            _geoParamsManager = geoParamsManager;
            _metrics = metrics;
            _checksumManager = checksumManager;
            _tcpServer = tcpServer;
            _udpServer = udpServer;
            _autostartServices = autostartServices;
            _kunosLobbyRegistration = kunosLobbyRegistration;
            _applicationLifetime = applicationLifetime;

            blacklistService.Blacklisted += OnBlacklisted;

            cspFeatureManager.Add(new CSPFeature { Name = "SPECTATING_AWARE" });
            cspFeatureManager.Add(new CSPFeature { Name = "LOWER_CLIENTS_SENDING_RATE" });

            if (_configuration.Extra.EnableClientMessages)
            {
                cspFeatureManager.Add(new CSPFeature { Name = "CLIENT_MESSAGES", Mandatory = true });
                CSPClientMessageOutgoing.ChatEncoded = false;
            }

            if (_configuration.Extra.EnableCustomUpdate)
            {
                cspFeatureManager.Add(new CSPFeature { Name = "CUSTOM_UPDATE" });
            }
        }

        private bool IsSessionOver()
        {
            if (_sessionManager.CurrentSession.Configuration.Type != SessionType.Race)
            {
                return (_sessionManager.ServerTimeMilliseconds - _sessionManager.CurrentSession.StartTimeMilliseconds) > 60_000 * _sessionManager.CurrentSession.Configuration.Time;
            }

            return false;
        }

        private void OnApplicationStopping()
        {
            Log.Information("Server shutting down");
            _entryCarManager.BroadcastPacket(new ChatMessage { SessionId = 255, Message = "*** Server shutting down ***" });
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Log.Information("Starting HTTP server on port {HttpPort}", _configuration.Server.HttpPort);
            
            _entryCarManager.Initialize();
            _checksumManager.Initialize();
            _sessionManager.Initialize();
            await _geoParamsManager.InitializeAsync();
            await _weatherManager.StartAsync(stoppingToken);
            await _tcpServer.StartAsync(stoppingToken);
            await _udpServer.StartAsync(stoppingToken);

            foreach (var service in _autostartServices)
            {
                await service.StartAsync(stoppingToken);
            }
            
            await _kunosLobbyRegistration.StartAsync(stoppingToken);

            _ = _applicationLifetime.ApplicationStopping.Register(OnApplicationStopping);
            _ = Task.Factory.StartNew(() => UpdateAsync(stoppingToken), TaskCreationOptions.LongRunning);
        }

        private void OnBlacklisted(IBlacklistService sender, BlacklistedEventArgs args)
        {
            string guidStr = args.Guid.ToString();
            
            foreach (var client in _entryCarManager.ConnectedCars.Values.Select(c => c.Client))
            {
                if (client != null && client.Guid != null && client.Guid == guidStr)
                {
                    client.Logger.Information("{ClientName} was banned after reloading blacklist", client.Name);
                    client.SendPacket(new KickCar {SessionId = client.SessionId, Reason = KickReason.VoteBlacklisted});
                    
                    _ = client.DisconnectAsync();
                }
            }
        }

        public void SendLapCompletedMessage(byte sessionId, int lapTime, int cuts, ACTcpClient? target = null)
        {
            if (_sessionManager.CurrentSession.Results == null)
                throw new ArgumentNullException(nameof(_sessionManager.CurrentSession.Results));
            
            var laps = _sessionManager.CurrentSession.Results
                .Select((result) => new LapCompletedOutgoing.CompletedLap
                {
                    SessionId = result.Key,
                    LapTime = _sessionManager.CurrentSession.Configuration.Type == SessionType.Race ? result.Value.TotalTime : result.Value.BestLap,
                    NumLaps = (short)result.Value.NumLaps,
                    HasCompletedLastLap = (byte)(result.Value.HasCompletedLastLap ? 1 : 0)
                })
                .OrderBy(lap => lap.LapTime); // TODO wrong for race sessions?

            var packet = new LapCompletedOutgoing
            {
                SessionId = sessionId,
                LapTime = lapTime,
                Cuts = (byte)cuts,
                Laps = laps.ToArray(),
                TrackGrip = _weatherManager.CurrentWeather.TrackGrip
            };

            if (target == null)
            {
                _entryCarManager.BroadcastPacket(packet);
            }
            else
            {
                target.SendPacket(packet);
            }
        }
        
        private async Task UpdateAsync(CancellationToken stoppingToken)
        {
            int failedUpdateLoops = 0;
            int sleepMs = 1000 / _configuration.Server.RefreshRateHz;
            long nextTick = _sessionManager.ServerTimeMilliseconds;
            Dictionary<EntryCar, CountedArray<PositionUpdateOut>> positionUpdates = new();
            foreach (var entryCar in _entryCarManager.EntryCars)
            {
                positionUpdates[entryCar] = new CountedArray<PositionUpdateOut>(_entryCarManager.EntryCars.Length);
            }

            Log.Information("Starting update loop with an update rate of {RefreshRateHz}hz", _configuration.Server.RefreshRateHz);

            var updateLoopTimer = new TimerOptions
            {
                Name = "ACServer.UpdateAsync",
                MeasurementUnit = Unit.Calls,
                DurationUnit = TimeUnit.Milliseconds,
                RateUnit = TimeUnit.Milliseconds
            };

            var updateLoopLateCounter = new CounterOptions
            {
                Name = "ACServer.UpdateAsync.Late",
                MeasurementUnit = Unit.None
            };
            _metrics.Measure.Counter.Increment(updateLoopLateCounter, 0);
            
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (_metrics.Measure.Timer.Time(updateLoopTimer))
                    {
                        Update?.Invoke(this, EventArgs.Empty);

                        for (int i = 0; i < _entryCarManager.EntryCars.Length; i++)
                        {
                            var fromCar = _entryCarManager.EntryCars[i];
                            var fromClient = fromCar.Client;
                            if (fromClient != null && fromClient.HasSentFirstUpdate && (_sessionManager.ServerTimeMilliseconds - fromCar.LastPingTime) > 1000)
                            {
                                fromCar.CheckAfk();
                                fromCar.LastPingTime = _sessionManager.ServerTimeMilliseconds;
                                fromClient.SendPacketUdp(new PingUpdate((uint)fromCar.LastPingTime, fromCar.Ping));

                                if (_sessionManager.ServerTimeMilliseconds - fromCar.LastPongTime > 15000)
                                {
                                    fromClient.Logger.Information("{ClientName} has not sent a ping response for over 15 seconds", fromClient.Name);
                                    _ = Task.Run(fromClient.DisconnectAsync, stoppingToken);
                                }
                            }

                            if (fromCar.AiControlled || fromCar.HasUpdateToSend)
                            {
                                fromCar.HasUpdateToSend = false;

                                for (int j = 0; j < _entryCarManager.EntryCars.Length; j++)
                                {
                                    var toCar = _entryCarManager.EntryCars[j];
                                    var toClient = toCar.Client;
                                    if (toCar == fromCar || toClient == null || !toClient.HasSentFirstUpdate
                                        || !fromCar.GetPositionUpdateForCar(toCar, out var update)) continue;

                                    if (toClient.SupportsCSPCustomUpdate || fromCar.AiControlled)
                                    {
                                        positionUpdates[toCar].Add(update);
                                    }
                                    else
                                    {
                                        toClient.SendPacketUdp(in update);
                                    }
                                }
                            }
                        }

                        foreach (var (toCar, updates) in positionUpdates)
                        {
                            if (updates.Count == 0) continue;
                            
                            var toClient = toCar.Client;
                            if (toClient != null)
                            {
                                const int chunkSize = 20;
                                for (int i = 0; i < updates.Count; i += chunkSize)
                                {
                                    if (toClient.SupportsCSPCustomUpdate)
                                    {
                                        var packet = new CSPPositionUpdate(new ArraySegment<PositionUpdateOut>(updates.Array, i, Math.Min(chunkSize, updates.Count - i)));
                                        toClient.SendPacketUdp(in packet);
                                    }
                                    else
                                    {
                                        var packet = new BatchedPositionUpdate((uint)(_sessionManager.ServerTimeMilliseconds - toCar.TimeOffset), toCar.Ping,
                                            new ArraySegment<PositionUpdateOut>(updates.Array, i, Math.Min(chunkSize, updates.Count - i)));
                                        toClient.SendPacketUdp(in packet);
                                    }
                                }
                            }
                            
                            updates.Clear();
                        }

                        if (IsSessionOver())
                        {
                            _sessionManager.NextSession();
                        }
                    }

                    if (_entryCarManager.ConnectedCars.Count > 0)
                    {
                        long tickDelta;
                        do
                        {
                            long currentTick = _sessionManager.ServerTimeMilliseconds;
                            tickDelta = nextTick - currentTick;

                            if (tickDelta > 0)
                                await Task.Delay((int)tickDelta, stoppingToken);
                            else if (tickDelta < -sleepMs)
                            {
                                if (tickDelta < -1000)
                                    Log.Warning("Server is running {TickDelta}ms behind", -tickDelta);

                                _metrics.Measure.Counter.Increment(updateLoopLateCounter, -tickDelta);
                                nextTick = 0;
                                break;
                            }
                        } while (tickDelta > 0);

                        if (nextTick == 0)
                            nextTick = _sessionManager.ServerTimeMilliseconds;

                        nextTick += sleepMs;
                    }
                    else
                    {
                        nextTick = _sessionManager.ServerTimeMilliseconds;
                        await Task.Delay(500, stoppingToken);
                    }

                    failedUpdateLoops = 0;
                }
                catch (TaskCanceledException) { }
                catch (Exception ex)
                {
                    if (failedUpdateLoops < 10)
                    {
                        failedUpdateLoops++;
                        Log.Error(ex, "Something went wrong while trying to do a tick update");
                    }
                    else
                    {
                        Log.Fatal(ex, "Cannot recover from update loop error, shutting down");
                        _applicationLifetime.StopApplication();
                    }
                }
            }
        }
    }
}
