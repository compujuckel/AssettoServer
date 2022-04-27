using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using App.Metrics;
using App.Metrics.Counter;
using App.Metrics.Timer;
using AssettoServer.Network.Tcp;
using AssettoServer.Network.Packets;
using AssettoServer.Server.Configuration;
using AssettoServer.Network.Packets.Shared;
using AssettoServer.Network.Packets.Outgoing;
using AssettoServer.Network.Udp;
using AssettoServer.Server.Blacklist;
using AssettoServer.Server.GeoParams;
using AssettoServer.Server.Plugin;
using AssettoServer.Server.Weather;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace AssettoServer.Server
{
    public class ACServer : BackgroundService
    {
        public CSPServerExtraOptions CSPServerExtraOptions { get; }
        internal GuidListFile Admins { get; } = new("admins.txt");
        internal Dictionary<uint, Action<ACTcpClient, PacketReader>> CSPClientMessageTypes { get; } = new();
        private List<PosixSignalRegistration> SignalHandlers { get; }

        private readonly ACPluginLoader _pluginLoader;
        private readonly ACServerConfiguration _configuration;
        private readonly SessionManager _sessionManager;
        private readonly EntryCarManager _entryCarManager;
        private readonly WeatherManager _weatherManager;
        private readonly GeoParamsManager _geoParamsManager;
        private readonly IMetricsRoot _metrics;
        private readonly IBlacklistService _blacklist;
        private readonly ChecksumManager _checksumManager;
        private readonly ACTcpServer _tcpServer;
        private readonly ACUdpServer _udpServer;
        private readonly KunosLobbyRegistration _lobby;

        /// <summary>
        /// Fires on each server tick in the main loop. Don't do resource intensive / long running stuff in here!
        /// </summary>
        public event EventHandler<ACServer, EventArgs>? Update;

        public ACServer(ACServerConfiguration configuration, 
            ACPluginLoader loader,
            IBlacklistService blacklistService,
            SessionManager sessionManager, 
            EntryCarManager entryCarManager, 
            WeatherManager weatherManager, 
            GeoParamsManager geoParamsManager, 
            IMetricsRoot metrics, 
            ChecksumManager checksumManager, 
            ACTcpServer tcpServer, 
            ACUdpServer udpServer, 
            KunosLobbyRegistration lobby, 
            CSPFeatureManager cspFeatureManager)
        {
            Log.Information("Starting server");
            
            _configuration = configuration;
            _blacklist = blacklistService;
            _sessionManager = sessionManager;
            _entryCarManager = entryCarManager;
            _weatherManager = weatherManager;
            _geoParamsManager = geoParamsManager;
            _metrics = metrics;
            _checksumManager = checksumManager;
            _tcpServer = tcpServer;
            _udpServer = udpServer;
            _lobby = lobby;

            CSPServerExtraOptions = new CSPServerExtraOptions(_configuration.WelcomeMessage);
            CSPServerExtraOptions.WelcomeMessage += LegalNotice.WelcomeMessage;
            if (_configuration.Extra.EnableCustomUpdate)
            {
                CSPServerExtraOptions.ExtraOptions += "\r\n" + $"[EXTRA_DATA]\r\nCUSTOM_UPDATE_FORMAT = '{CSPPositionUpdate.CustomUpdateFormat}'";
            }
            CSPServerExtraOptions.ExtraOptions += "\r\n" + _configuration.CSPExtraOptions;

            _blacklist.Blacklisted += OnBlacklisted;
            _pluginLoader = loader;
            
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

            SignalHandlers = new List<PosixSignalRegistration>()
            {
                PosixSignalRegistration.Create(PosixSignal.SIGINT, TerminateHandler),
                PosixSignalRegistration.Create(PosixSignal.SIGQUIT, TerminateHandler),
                PosixSignalRegistration.Create(PosixSignal.SIGTERM, TerminateHandler),
                PosixSignalRegistration.Create(PosixSignal.SIGHUP, TerminateHandler),
            };

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                SignalHandlers.Add(PosixSignalRegistration.Create((PosixSignal)10 /* SIGUSR1 */, ReloadHandler));
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

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _geoParamsManager.InitializeAsync();
            await Admins.LoadAsync();
            _entryCarManager.Initialize();
            _checksumManager.Initialize();
            _sessionManager.Initialize();
            await _tcpServer.StartAsync(stoppingToken);
            await _udpServer.StartAsync(stoppingToken);

            if (_configuration.Server.RegisterToLobby)
            {
                await _lobby.StartAsync(stoppingToken);
            }

            if (!_configuration.Extra.UseSteamAuth && Admins.List.Any())
            {
                const string errorMsg =
                    "Admin whitelist is enabled but Steam auth is disabled. This is unsafe because it allows players to gain admin rights by SteamID spoofing. More info: https://github.com/compujuckel/AssettoServer/wiki/Common-configuration-errors#unsafe-admin-whitelist";
                if (_configuration.Extra.IgnoreConfigurationErrors.UnsafeAdminWhitelist)
                {
                    Log.Warning(errorMsg);
                }
                else
                {
                    throw new ConfigurationException(errorMsg);
                }
            }

            for (var i = 0; i < _entryCarManager.EntryCars.Length; i++)
            {
                _entryCarManager.EntryCars[i].ResetLogger();
            }

            Log.Information("Starting HTTP server on port {HttpPort}", _configuration.Server.HttpPort);

            _ = Task.Factory.StartNew(() => UpdateAsync(stoppingToken), TaskCreationOptions.LongRunning);
            
            foreach (var plugin in _pluginLoader.LoadedPlugins)
            {
                plugin.Instance.Initialize(this);
            }
        }
        
        private void TerminateHandler(PosixSignalContext context)
        {
            Log.Information("Caught signal, server shutting down");
            _entryCarManager.BroadcastPacket(new ChatMessage { SessionId = 255, Message = "*** Server shutting down ***" });
            
            // Allow some time for the chat messages to be sent
            Thread.Sleep(250);
            Log.CloseAndFlush();
        }
        
        private void ReloadHandler(PosixSignalContext context)
        {
            Log.Information("Reloading adminlist...");
            _ = Admins.LoadAsync();
            context.Cancel = true;
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

        [SuppressMessage("ReSharper", "FunctionNeverReturns")]
        private async Task UpdateAsync(CancellationToken stoppingToken)
        {
            int failedUpdateLoops = 0;
            int sleepMs = 1000 / _configuration.Server.RefreshRateHz;
            long nextTick = _sessionManager.ServerTimeMilliseconds;
            long lastTimeUpdate = nextTick;
            Dictionary<EntryCar, CountedArray<PositionUpdateOut>> positionUpdates = new();
            foreach (var entryCar in _entryCarManager.EntryCars)
            {
                positionUpdates[entryCar] = new CountedArray<PositionUpdateOut>(_entryCarManager.EntryCars.Length);
            }

            Log.Information("Starting update loop with an update rate of {RefreshRateHz}hz", _configuration.Server.RefreshRateHz);

            var timerOptions = new TimerOptions
            {
                Name = "ACServer.UpdateAsync",
                MeasurementUnit = Unit.Calls,
                DurationUnit = TimeUnit.Milliseconds,
                RateUnit = TimeUnit.Milliseconds
            };

            var updateLoopLateOptions = new CounterOptions
            {
                Name = "ACServer.UpdateAsync.Late",
                MeasurementUnit = Unit.None
            };
            _metrics.Measure.Counter.Increment(updateLoopLateOptions, 0);
            
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (_metrics.Measure.Timer.Time(timerOptions))
                    {
                        Update?.Invoke(this, EventArgs.Empty);

                        for (int i = 0; i < _entryCarManager.EntryCars.Length; i++)
                        {
                            var fromCar = _entryCarManager.EntryCars[i];
                            var fromClient = fromCar.Client;
                            if (fromClient != null && fromClient.HasSentFirstUpdate && (_sessionManager.ServerTimeMilliseconds - fromCar.LastPingTime) > 1000)
                            {
                                fromCar.CheckAfk();
                                fromCar.LastPingTime = (int)_sessionManager.ServerTimeMilliseconds;
                                fromClient.SendPacketUdp(new PingUpdate(fromCar.LastPingTime, fromCar.Ping));

                                if (_sessionManager.ServerTimeMilliseconds - fromCar.LastPongTime > 15000)
                                {
                                    fromClient.Logger.Information("{ClientName} has not sent a ping response for over 15 seconds", fromClient.Name);
                                    _ = fromClient.DisconnectAsync();
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
                                await Task.Delay((int)tickDelta);
                            else if (tickDelta < -sleepMs)
                            {
                                if (tickDelta < -1000)
                                    Log.Warning("Server is running {TickDelta}ms behind", -tickDelta);

                                _metrics.Measure.Counter.Increment(updateLoopLateOptions, -tickDelta);
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
                        await Task.Delay(500);
                    }

                    failedUpdateLoops = 0;
                }
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
                        Log.CloseAndFlush();
                        Environment.Exit(1);
                    }
                }
            }
        }

        public void RegisterCSPClientMessageType(uint type, Action<ACTcpClient, PacketReader> handler)
        {
            if (CSPClientMessageTypes.ContainsKey(type))
                throw new ArgumentException($"Type {type} already registered");

            CSPClientMessageTypes.Add(type, handler);
        }
    }
}
