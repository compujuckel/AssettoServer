using System;
using System.IO;
using System.Net;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Threading;
using System.Reflection;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using App.Metrics;
using App.Metrics.AspNetCore;
using App.Metrics.Formatters.Prometheus;
using App.Metrics.Timer;
using AssettoServer.Commands;
using AssettoServer.Network.Udp;
using AssettoServer.Network.Tcp;
using AssettoServer.Network.Http;
using AssettoServer.Network.Packets;
using AssettoServer.Commands.TypeParsers;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Weather;
using AssettoServer.Network.Packets.Shared;
using AssettoServer.Network.Packets.Incoming;
using AssettoServer.Network.Packets.Outgoing;
using AssettoServer.Server.Ai;
using AssettoServer.Server.Plugin;
using AssettoServer.Server.TrackParams;
using AssettoServer.Server.Weather.Implementation;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Serilog;
using Qmmands;
using Steamworks;
using Newtonsoft.Json;
using TimeZoneConverter;

namespace AssettoServer.Server
{
    public class ACServer
    {
        public ACServerConfiguration Configuration { get; }
        public SessionConfiguration CurrentSession { get; private set; }
        public WeatherData CurrentWeather { get; private set; }
        public DateTime CurrentDateTime { get; set; }
        public TimeZoneInfo TimeZone { get; }
        public GeoParams GeoParams { get; private set; }
        public IReadOnlyList<string> Features { get; private set; }
        public IMetricsRoot Metrics { get; }

        internal ConcurrentDictionary<int, EntryCar> ConnectedCars { get; }
        internal ConcurrentDictionary<IPEndPoint, EntryCar> EndpointCars { get; }
        internal ImmutableList<EntryCar> EntryCars { get; }
        internal ConcurrentDictionary<string, bool> Blacklist { get; }
        internal ConcurrentDictionary<string, bool> Admins { get; }
        internal IReadOnlyDictionary<string, byte[]> TrackChecksums { get; private set; }
        internal IReadOnlyDictionary<string, byte[]> CarChecksums { get; private set; }
        internal CommandService CommandService { get; }
        internal TcpListener TcpListener { get; set; }
        internal ACUdpServer UdpServer { get; }
        internal IWebHost HttpServer { get; private set; }
        internal KunosLobbyRegistration KunosLobbyRegistration { get; }

        internal int StartTime { get; } = Environment.TickCount;
        internal int CurrentTime => Environment.TickCount - StartTime;
        internal long StartTime64 { get; } = Environment.TickCount64;
        internal long CurrentTime64 => Environment.TickCount64 - StartTime64;

        private SemaphoreSlim ConnectSemaphore { get; }
        private HttpClient HttpClient { get; }
        public IWeatherTypeProvider WeatherTypeProvider { get; }
        public DefaultWeatherProvider WeatherProvider { get; }
        public IWeatherImplementation WeatherImplementation { get; }
        private RainHelper RainHelper { get; }
        private ITrackParamsProvider TrackParamsProvider { get; }
        public TrackParams.TrackParams TrackParams { get; }
        
        public TrafficMap TrafficMap { get; }
        public AiBehavior AiBehavior { get; }
        
        public ACPluginLoader PluginLoader { get; }
        
        private List<PosixSignalRegistration> SignalHandlers { get; }

        public event EventHandler<ACTcpClient, ClientHandshakeEventArgs> ClientHandshakeStarted; 
        public event EventHandler<ACTcpClient, EventArgs> ClientChecksumPassed;
        public event EventHandler<ACTcpClient, EventArgs> ClientChecksumFailed;
        public event EventHandler<ACTcpClient, EventArgs> ClientDisconnected;
        public event EventHandler<ACTcpClient, ClientAuditEventArgs> ClientKicked;
        public event EventHandler<ACTcpClient, ClientAuditEventArgs> ClientBanned;
        public event EventHandler<ACServer, EventArgs> Update;
        public event EventHandler<ACTcpClient, ChatEventArgs> ChatMessageReceived;

        public ACServer(ACServerConfiguration configuration, ACPluginLoader loader)
        {
            Log.Information("Starting server.");

            Metrics = new MetricsBuilder()
                .Configuration.Configure(options => { options.DefaultContextLabel = "AssettoServer"; })
                .OutputMetrics.AsPrometheusPlainText()
                .OutputMetrics.AsPrometheusProtobuf()
                .Build();

            Configuration = configuration;
            EntryCars = Configuration.EntryCars.ToImmutableList();
            Log.Information("Loaded {0} cars.", EntryCars.Count);
            for (int i = 0; i < EntryCars.Count; i++)
            {
                EntryCars[i].SessionId = (byte)i;
                EntryCars[i].Server = this;
                EntryCars[i].OtherCarsLastSentUpdateTime = new long[EntryCars.Count];
                EntryCars[i].AiInit();
            }
            
            ConnectSemaphore = new SemaphoreSlim(1, 1);
            ConnectedCars = new ConcurrentDictionary<int, EntryCar>();
            EndpointCars = new ConcurrentDictionary<IPEndPoint, EntryCar>();
            Blacklist = new ConcurrentDictionary<string, bool>();
            Admins = new ConcurrentDictionary<string, bool>();
            UdpServer = new ACUdpServer(this, Configuration.UdpPort);
            HttpClient = new HttpClient();
            KunosLobbyRegistration = new KunosLobbyRegistration(this); 
            PluginLoader = loader;
            CommandService = new CommandService(new CommandServiceConfiguration
            {
                DefaultRunMode = RunMode.Parallel
            });

            CommandService.AddModules(Assembly.GetEntryAssembly());
            CommandService.AddTypeParser(new ACClientTypeParser());
            CommandService.CommandExecutionFailed += OnCommandExecutionFailed;

            foreach (var plugin in PluginLoader.LoadedPlugins)
            { 
                CommandService.AddModules(plugin.Assembly);
            }

            var features = new List<string>();
            if (Configuration.Extra.UseSteamAuth)
                features.Add("STEAM_TICKET");
            
            if(Configuration.Extra.EnableWeatherFx)
                features.Add("WEATHERFX_V1");

            features.Add("SPECTATING_AWARE");
            features.Add("LOWER_CLIENTS_SENDING_RATE");
            features.Add("CLIENTS_EXCHANGE_V1");

            Features = features;

            TrackParamsProvider = new IniTrackParamsProvider();
            TrackParamsProvider.Initialize().Wait();
            TrackParams = TrackParamsProvider.GetParamsForTrack(Configuration.Track);

            if (TrackParams == null)
            {
                Log.Error("No track params found for {0}. Features requiring track coordinates or time zone will not work", Configuration.Track);
                TimeZone = TimeZoneInfo.Utc;
            }
            else
            {
                /*
                * In theory TZConvert could be removed because .NET 6 supports IANA timezone names natively
                * In practice the native way is not supported in Windows 10 LTSC 2019, so keeping this in for now
                * https://docs.microsoft.com/en-us/windows/win32/intl/international-components-for-unicode--icu-
                * ("icu.dll" is required which was added in Version 1903)
                */
                TimeZone = TZConvert.GetTimeZoneInfo(TrackParams.Timezone);
            }

            var startDate = new DateTime(DateTime.UtcNow.Date.Ticks);
            CurrentDateTime = TimeZoneInfo.ConvertTimeToUtc(startDate + TimeSpan.FromSeconds(WeatherUtils.SecondsFromSunAngle(Configuration.SunAngle)), TimeZone);
            
            WeatherTypeProvider = new DefaultWeatherTypeProvider();
            RainHelper = new RainHelper();

            if (Configuration.Extra.EnableWeatherFx)
            {
                WeatherImplementation = new WeatherFxV1Implementation(this);
            }
            else
            {
                WeatherImplementation = new VanillaWeatherImplementation(this);
            }
            
            WeatherProvider = new DefaultWeatherProvider(this);

            if (Configuration.Extra.EnableAi)
            {
                string mapAiBasePath = "content/tracks/" + Configuration.Track + "/ai/";
                if (File.Exists(mapAiBasePath + "traffic_map.obj"))
                {
                    TrafficMap = WavefrontObjParser.ParseFile(mapAiBasePath + "traffic_map.obj", Configuration.Extra.AiParams.LaneWidthMeters);
                } 
                else
                {
                    var parser = new FastLaneParser(this);
                    TrafficMap = parser.FromFiles(mapAiBasePath);
                }

                if (TrafficMap == null)
                {
                    Log.Error("AI enabled but no AI spline found. Disabling AI");
                    Configuration.Extra.EnableAi = false;
                }
                else
                {
                    AiBehavior = new AiBehavior(this);
                }
            }

            InitializeChecksums();

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

        private async Task LoadAdminsAsync()
        {
            try
            {
                await ConnectSemaphore.WaitAsync();
                
                const string adminsPath = "admins.txt";
                if (File.Exists(adminsPath))
                {
                    Admins.Clear();
                    foreach (string guid in await File.ReadAllLinesAsync(adminsPath))
                        Admins[guid] = true;
                }
                else
                    File.Create(adminsPath);
            }
            finally
            {
                ConnectSemaphore.Release();
            }
        }

        private async Task LoadBlacklistAsync()
        {
            try
            {
                await ConnectSemaphore.WaitAsync();
                
                const string blacklistPath = "blacklist.txt";
                if (File.Exists(blacklistPath))
                {
                    Blacklist.Clear();
                    foreach (string guid in await File.ReadAllLinesAsync(blacklistPath))
                        Blacklist[guid] = true;
                }
                else
                    File.Create(blacklistPath);
            }
            finally
            {
                ConnectSemaphore.Release();
            }
        }

        public async Task StartAsync()
        {
            CurrentSession = Configuration.Sessions[0];
            CurrentSession.StartTime = DateTime.Now;
            CurrentSession.StartTimeTicks = CurrentTime;

            await LoadBlacklistAsync();
            await LoadAdminsAsync();
            
            await InitializeGeoParams();

            InitializeSteam();
            _ = Task.Factory.StartNew(AcceptTcpConnectionsAsync, TaskCreationOptions.LongRunning);
            UdpServer.Start();

            Log.Information("Starting HTTP server on port {0}", Configuration.HttpPort);
            
            HttpServer = WebHost.CreateDefaultBuilder()
                .ConfigureKestrel(options => options.AllowSynchronousIO = true)
                .ConfigureMetrics(Metrics)
                .UseMetrics(options => { options.EndpointOptions = endpointsOptions => { endpointsOptions.MetricsEndpointOutputFormatter = Metrics.OutputMetricsFormatters.OfType<MetricsPrometheusTextOutputFormatter>().First(); }; })
                .UseSerilog()
                .UseStartup(_ => new Startup(this))
                .UseUrls($"http://*:{Configuration.HttpPort}")
                .Build();
            await HttpServer.StartAsync();
            
            if (Configuration.RegisterToLobby)
                _ = KunosLobbyRegistration.LoopAsync();
            
            _ = Task.Factory.StartNew(UpdateAsync, TaskCreationOptions.LongRunning);
        }

        private async Task InitializeGeoParams()
        {
            try
            {
                HttpResponseMessage response = await HttpClient.GetAsync("http://ip-api.com/json");

                if (response.IsSuccessStatusCode)
                {
                    string jsonString = await response.Content.ReadAsStringAsync();
                    Dictionary<string, string> json = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonString);
                    GeoParams = new GeoParams
                    {
                        Ip = json["query"],
                        City = json["city"],
                        Country = json["country"],
                        CountryCode = json["countryCode"]
                    };
                }
                else
                {
                    Log.Error("Failed to get IP geolocation parameters");
                    GeoParams = new GeoParams();
                }
            }
            catch (HttpRequestException e)
            {
                Log.Error(e, "Failed to get IP geolocation parameters");
                GeoParams = new GeoParams();
            }
        }

        private void TerminateHandler(PosixSignalContext context)
        {
            Log.Information("Caught signal, server shutting down");
            BroadcastPacket(new ChatMessage { SessionId = 255, Message = "*** Server shutting down ***" });
                
            // Allow some time for the chat messages to be sent
            Thread.Sleep(250);
        }
        
        private void ReloadHandler(PosixSignalContext context)
        {
            Log.Information("Reloading blacklist and adminlist...");
            
            LoadBlacklistAsync().ContinueWith(t => Log.Error(t.Exception, "Error reloading blacklist"), TaskContinuationOptions.OnlyOnFaulted);
            LoadAdminsAsync().ContinueWith(t => Log.Error(t.Exception, "Error reloading adminlist"), TaskContinuationOptions.OnlyOnFaulted);
            
            context.Cancel = true;
        }

        private void InitializeChecksums()
        {
            Log.Information("Initializing checksums...");

            using (MD5 md5 = MD5.Create())
            {
                Dictionary<string, byte[]> checksums = new Dictionary<string, byte[]>();

                byte[] createChecksum(string filePath)
                {
                    if (File.Exists(filePath))
                    {
                        using (FileStream fileStream = File.OpenRead(filePath))
                            return md5.ComputeHash(fileStream);
                    }

                    return null;
                }

                void addChecksum(string filePath, string name)
                {
                    byte[] checksum = createChecksum(filePath);
                    if (checksum != null)
                        checksums[name] = checksum;
                }

                void checksumDirectory(string directory)
                {
                    foreach (string dir in Directory.GetDirectories(directory))
                        checksumDirectory(dir);

                    string[] allFiles = Directory.GetFiles(directory);
                    foreach (string file in allFiles)
                    {
                        string name = Path.GetFileName(file);

                        if (name == "surfaces.ini" || name.StartsWith("models_"))
                            addChecksum(file, file.Replace("\\", "/"));
                    }
                }

                createChecksum("system/data/surfaces.ini");
                checksumDirectory("content/tracks/" + (string.IsNullOrEmpty(Configuration.TrackConfig) ? Configuration.Track : Configuration.Track + "/" + Configuration.TrackConfig));

                TrackChecksums = checksums;
                checksums = new Dictionary<string, byte[]>();

                foreach (EntryCar car in Configuration.EntryCars)
                    addChecksum($"content/cars/{car.Model}/data.acd", car.Model);

                CarChecksums = checksums;

                Log.Information("Initialized {0} checksums.", CarChecksums.Count + TrackChecksums.Count);
            }
        }

        public bool IsGuidBlacklisted(string guid)
        {
            return Blacklist.ContainsKey(guid);
        }

        public async Task AddAdminAsync(string guid)
        {
            if (Admins.TryAdd(guid, true))
            {
                await File.WriteAllLinesAsync("admins.txt", Admins.Select(p => p.Key));
                Log.Information("Id {0} has been added as an admin.", guid);
            }
        }

        public async Task RemoveAdminAsync(string guid)
        {
            if (Admins.TryRemove(guid, out _))
            {
                await File.WriteAllLinesAsync("admins.txt", Admins.Select(p => p.Key));
                Log.Information("Id {0} has been removed as an admin.", guid);
            }
        }

        public async Task<bool> TrySecureSlotAsync(ACTcpClient client, HandshakeRequest handshakeRequest)
        {
            try
            {
                await ConnectSemaphore.WaitAsync();

                if (ConnectedCars.Count >= Configuration.MaxClients)
                    return false;

                for (int i = 0; i < EntryCars.Count; i++)
                {
                    EntryCar entryCar = EntryCars[i];
                    if (entryCar.Client != null && entryCar.Client.Guid == client.Guid)
                        return false;

                    var isAdmin = !string.IsNullOrEmpty(handshakeRequest.Guid) && Admins.ContainsKey(handshakeRequest.Guid);
                    
                    if (entryCar.AiMode != AiMode.Fixed 
                        && (isAdmin || Configuration.Extra.AiParams.MaxPlayerCount == 0 || ConnectedCars.Count < Configuration.Extra.AiParams.MaxPlayerCount) 
                        && entryCar.Client == null && handshakeRequest.RequestedCar == entryCar.Model)
                    {
                        entryCar.Reset();
                        entryCar.Client = client;
                        client.EntryCar = entryCar;
                        client.SessionId = entryCar.SessionId;
                        client.IsConnected = true;
                        client.IsAdministrator = isAdmin;

                        ConnectedCars[client.SessionId] = entryCar;

                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error securing slot for {0}.", client.Name);
            }
            finally
            {
                ConnectSemaphore.Release();
            }

            return false;
        }

        public async Task KickAsync(ACTcpClient client, KickReason reason, string reasonStr = null, bool broadcastMessage = true, ACTcpClient admin = null)
        {
            if (reasonStr != null && broadcastMessage)
                BroadcastPacket(new ChatMessage {SessionId = 255, Message = reasonStr});

            if (client != null)
            {
                Log.Information("{0} was kicked. Reason: {1}", client.Name, reasonStr ?? "No reason given.");
                client.SendPacket(new KickCar {SessionId = client.SessionId, Reason = reason});

                var args = new ClientAuditEventArgs
                {
                    Reason = reason,
                    ReasonStr = reasonStr,
                    Admin = admin
                };
                ClientKicked?.Invoke(client, args);
                
                await Task.Delay(250);
                await client.DisconnectAsync();
            }
        }

        public async Task BanAsync(ACTcpClient client, KickReason reason, string reasonStr = null, ACTcpClient admin = null)
        {
            if (reasonStr != null)
                BroadcastPacket(new ChatMessage {SessionId = 255, Message = reasonStr});

            if (client != null)
            {
                Blacklist.TryAdd(client.Guid, true);

                Log.Information("{0} was banned. Reason: {1}", client.Name, reasonStr ?? "No reason given.");
                await File.WriteAllLinesAsync("blacklist.txt", Blacklist.Where(p => p.Value).Select(p => p.Key));
                client.SendPacket(new KickCar {SessionId = client.SessionId, Reason = reason});

                var args = new ClientAuditEventArgs
                {
                    Reason = reason,
                    ReasonStr = reasonStr,
                    Admin = admin
                };
                ClientBanned?.Invoke(client, args);

                await Task.Delay(250);
                await client.DisconnectAsync();
            }
        }

        public async ValueTask UnbanAsync(string guid)
        {
            if (Blacklist.TryRemove(guid, out _))
                await File.WriteAllLinesAsync("blacklist.txt", Blacklist.Where(p => p.Value).Select(p => p.Key));
        }

        public void SetWeather(WeatherData weather)
        {
            CurrentWeather = weather;
            WeatherImplementation.SendWeather();
        }
        
        public void SetCspWeather(WeatherFxType upcoming, int duration)
        {
            Log.Information("CSP weather transitioning to {0}", upcoming);
            
            CurrentWeather.UpcomingType = WeatherTypeProvider.GetWeatherType(upcoming);
            CurrentWeather.TransitionValue = 0;
            CurrentWeather.TransitionValueInternal = 0;
            CurrentWeather.TransitionDuration = duration * 1000;

            WeatherImplementation.SendWeather();
        }

        public void SetTime(float time)
        {
            CurrentDateTime = TimeZoneInfo.ConvertTimeToUtc(TimeZoneInfo.ConvertTimeFromUtc(CurrentDateTime, TimeZone).Date + TimeSpan.FromSeconds(time), TimeZone);
        }

        public void BroadcastPacket<TPacket>(TPacket packet, ACTcpClient sender = null) where TPacket : IOutgoingNetworkPacket
        {
            if (!(packet is SunAngleUpdate))
                Log.Verbose("Broadcasting {0}", typeof(TPacket).Name);

            foreach (EntryCar car in EntryCars.Where(c => c.Client != null && c.Client.HasSentFirstUpdate && sender != c.Client))
                car.Client.SendPacket(packet);
        }
        
        public void BroadcastPacketUdp<TPacket>(TPacket packet, ACTcpClient sender = null) where TPacket : IOutgoingNetworkPacket
        {
            if (!(packet is SunAngleUpdate))
                Log.Verbose("Broadcasting {0}", typeof(TPacket).Name);

            foreach (EntryCar car in EntryCars.Where(c => c.Client != null && c.Client.HasSentFirstUpdate && sender != c.Client && c.Client.HasAssociatedUdp))
                car.Client.SendPacketUdp(packet);
        }

        public async Task UpdateAsync()
        {
            int sleepMs = 1000 / Configuration.RefreshRateHz;
            long nextTick = Environment.TickCount64;
            byte[] buffer = new byte[2048];
            long lastTimeUpdate = Environment.TickCount64;
            float networkDistanceSquared = (float)Math.Pow(Configuration.Extra.NetworkBubbleDistance, 2);
            int outsideNetworkBubbleUpdateRateMs = 1000 / Configuration.Extra.OutsideNetworkBubbleRefreshRateHz;

            Log.Information("Starting update loop with an update rate of {0}hz.", Configuration.RefreshRateHz);

            var timerOptions = new TimerOptions
            {
                Name = "ACServer.UpdateAsync",
                MeasurementUnit = Unit.Calls,
                DurationUnit = TimeUnit.Milliseconds,
                RateUnit = TimeUnit.Milliseconds
            };
            
            while (true)
            {
                try
                {
                    using (var timer = Metrics.Measure.Timer.Time(timerOptions))
                    {
                        Update?.Invoke(this, EventArgs.Empty);

                        foreach (EntryCar fromCar in EntryCars)
                        {
                            ACTcpClient fromClient = fromCar.Client;
                            if (!fromCar.AiControlled && fromClient != null)
                            {
                                if (fromCar.HasUpdateToSend)
                                {
                                    fromCar.HasUpdateToSend = false;

                                    CarStatus status = fromCar.Status;

                                    foreach (EntryCar toCar in EntryCars)
                                    {
                                        ACTcpClient toClient = toCar.Client;
                                        if (toCar != fromCar && toClient != null && toClient.HasSentFirstUpdate)
                                        {
                                            float distance = Vector3.DistanceSquared(status.Position, toCar.TargetCar == null ? toCar.Status.Position : toCar.TargetCar.Status.Position);
                                            if (fromCar.TargetCar != null || distance > networkDistanceSquared)
                                            {
                                                if ((Environment.TickCount64 - fromCar.OtherCarsLastSentUpdateTime[toCar.SessionId]) < outsideNetworkBubbleUpdateRateMs)
                                                    continue;

                                                fromCar.OtherCarsLastSentUpdateTime[toCar.SessionId] = Environment.TickCount64;
                                            }

                                            toClient.SendPacketUdp(new PositionUpdate
                                            {
                                                SessionId = fromCar.SessionId,
                                                EngineRpm = status.EngineRpm,
                                                Gas = status.Gas,
                                                Gear = status.Gear,
                                                LastRemoteTimestamp = fromCar.LastRemoteTimestamp,
                                                Timestamp = (uint)(fromCar.Status.Timestamp - toCar.TimeOffset),
                                                NormalizedPosition = status.NormalizedPosition,
                                                PakSequenceId = status.PakSequenceId,
                                                PerformanceDelta = status.PerformanceDelta,
                                                Ping = fromCar.Ping,
                                                Position = status.Position,
                                                Rotation = status.Rotation,
                                                StatusFlag = (Configuration.Extra.ForceLights || fromCar.ForceLights)
                                                    ? status.StatusFlag | CarStatusFlags.LightsOn
                                                    : status.StatusFlag,
                                                SteerAngle = status.SteerAngle,
                                                TyreAngularSpeedFL = status.TyreAngularSpeed[0],
                                                TyreAngularSpeedFR = status.TyreAngularSpeed[1],
                                                TyreAngularSpeedRL = status.TyreAngularSpeed[2],
                                                TyreAngularSpeedRR = status.TyreAngularSpeed[3],
                                                Velocity = status.Velocity,
                                                WheelAngle = status.WheelAngle
                                            });
                                        }
                                    }

                                    if (fromCar.Status.Velocity.Y < -75 && Environment.TickCount64 - fromCar.LastFallCheckTime > 1000)
                                    {
                                        fromCar.LastFallCheckTime = Environment.TickCount64;
                                        fromCar.Client?.SendCurrentSession();
                                    }
                                }

                                if (fromClient != null && fromClient.HasSentFirstUpdate && (CurrentTime - fromCar.LastPingTime) > 1000)
                                {
                                    fromCar.CheckAfk();
                                    fromCar.LastPingTime = CurrentTime;

                                    PacketWriter writer = new PacketWriter(buffer);
                                    int bytesWritten = writer.WritePacket(new PingUpdate { CurrentPing = fromCar.Ping, Time = CurrentTime });

                                    UdpServer.Send(fromClient.UdpEndpoint, buffer, 0, bytesWritten);

                                    if (CurrentTime - fromCar.LastPongTime > 15000)
                                    {
                                        Log.Information("{0} has not sent a ping response for over 15 seconds.", fromCar?.Client?.Name);
                                        _ = fromCar.Client?.DisconnectAsync();
                                    }
                                }
                            }
                            else if (fromCar.AiControlled)
                            {
                                foreach (EntryCar toCar in EntryCars)
                                {
                                    ACTcpClient toClient = toCar.Client;
                                    if (toCar != fromCar && toClient != null && toClient.HasSentFirstUpdate)
                                    {
                                        var targetCarStatus = toCar.TargetCar == null ? toCar.Status : toCar.TargetCar.Status;

                                        AiState aiState = fromCar.GetBestStateForPlayer(targetCarStatus);

                                        if (aiState == null) continue;

                                        if (fromCar.LastSeenAiState[toCar.SessionId] != aiState
                                            || fromCar.LastSeenAiSpawn[toCar.SessionId] != aiState.SpawnCounter)
                                        {
                                            fromCar.LastSeenAiState[toCar.SessionId] = aiState;
                                            fromCar.LastSeenAiSpawn[toCar.SessionId] = aiState.SpawnCounter;

                                            toClient.SendPacket(new CSPCarColorUpdate
                                            {
                                                SessionId = fromCar.SessionId,
                                                Color = aiState.Color
                                            });
                                        }

                                        var status = aiState.Status;
                                        toClient.SendPacketUdp(new PositionUpdate
                                        {
                                            SessionId = fromCar.SessionId,
                                            EngineRpm = status.EngineRpm,
                                            Gas = status.Gas,
                                            Gear = status.Gear,
                                            LastRemoteTimestamp = (uint)status.Timestamp,
                                            Timestamp = (uint)(status.Timestamp - toCar.TimeOffset),
                                            NormalizedPosition = status.NormalizedPosition,
                                            PakSequenceId = fromCar.AiPakSequenceIds[toCar.SessionId]++,
                                            PerformanceDelta = status.PerformanceDelta,
                                            Ping = fromCar.Ping,
                                            Position = status.Position,
                                            Rotation = status.Rotation,
                                            StatusFlag = (Configuration.Extra.ForceLights || fromCar.ForceLights)
                                                ? status.StatusFlag | CarStatusFlags.LightsOn
                                                : status.StatusFlag,
                                            SteerAngle = status.SteerAngle,
                                            TyreAngularSpeedFL = status.TyreAngularSpeed[0],
                                            TyreAngularSpeedFR = status.TyreAngularSpeed[1],
                                            TyreAngularSpeedRL = status.TyreAngularSpeed[2],
                                            TyreAngularSpeedRR = status.TyreAngularSpeed[3],
                                            Velocity = status.Velocity,
                                            WheelAngle = status.WheelAngle
                                        });
                                    }
                                }
                            }
                        }

                        if (Environment.TickCount64 - lastTimeUpdate > 1000)
                        {
                            if (Configuration.Extra.EnableRealTime)
                            {
                                CurrentDateTime = DateTime.UtcNow;
                            }
                            else
                            {
                                CurrentDateTime += TimeSpan.FromMilliseconds((Environment.TickCount64 - lastTimeUpdate) * Configuration.TimeOfDayMultiplier);
                            }

                            RainHelper.Update(CurrentWeather, Configuration.DynamicTrack.BaseGrip, Configuration.Extra.RainTrackGripReductionPercent, Environment.TickCount64 - lastTimeUpdate);
                            WeatherImplementation.SendWeather();

                            lastTimeUpdate = Environment.TickCount64;
                        }

                        if (CurrentSession.TimeLeft.TotalMilliseconds < 100)
                        {
                            CurrentSession.StartTime = DateTime.Now;
                            CurrentSession.StartTimeTicks = CurrentTime64;

                            foreach (EntryCar car in EntryCars.Where(c => c.Client != null))
                            {
                                car.Client.SendCurrentSession();
                                Log.Information("Restarting session for {0}.", car.Client.Name);
                            }
                        }
                    }

                    if (ConnectedCars.Count > 0)
                    {
                        long tickDelta;
                        do
                        {
                            long currentTick = Environment.TickCount64;
                            tickDelta = nextTick - currentTick;

                            if (tickDelta > 0)
                                await Task.Delay((int)tickDelta);
                            else if (tickDelta < -sleepMs)
                            {
                                if (tickDelta < -1000)
                                    Log.Warning("Server is running {0}ms behind.", -tickDelta);

                                nextTick = 0;
                                break;
                            }
                        } while (tickDelta > 0);

                        if (nextTick == 0)
                            nextTick = Environment.TickCount64;

                        nextTick += sleepMs;
                    }
                    else
                    {
                        nextTick = Environment.TickCount64;
                        await Task.Delay(500);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Something went wrong while trying to do a tick update.");
                }
            }
        }

        internal async Task DisconnectClientAsync(ACTcpClient client)
        {
            try
            {
                await ConnectSemaphore.WaitAsync();
                if (client.IsConnected && client.EntryCar?.Client == client && ConnectedCars.TryRemove(client.SessionId, out _))
                {
                    Log.Information("{0} has disconnected.", client.Name);

                    if (client.UdpEndpoint != null)
                        EndpointCars.TryRemove(client.UdpEndpoint, out _);

                    client.EntryCar.Client = null;
                    client.IsConnected = false;

                    if (client.HasPassedChecksum)
                        BroadcastPacket(new CarDisconnected { SessionId = client.SessionId });
                    
                    ClientDisconnected?.Invoke(client, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error disconnecting {0}.", client?.Name);
            }
            finally
            {
                ConnectSemaphore.Release();
            }
        }

        internal async Task ProcessCommandAsync(ACTcpClient client, ChatMessage message)
        {
            ACCommandContext context = new ACCommandContext(this, client, message);
            IResult result = await CommandService.ExecuteAsync(message.Message, context);

            if (result is ChecksFailedResult checksFailedResult)
                context.Reply(checksFailedResult.FailedChecks[0].Result.FailureReason);
            else if (result is FailedResult failedResult)
                context.Reply(failedResult.FailureReason);
        }

        private Task OnCommandExecutionFailed(CommandExecutionFailedEventArgs e)
        {
            if (!e.Result.IsSuccessful)
            {
                (e.Context as ACCommandContext).Reply("An error occurred while executing this command.");
                Log.Error(e.Result.Exception, e.Result.FailureReason);
            }

            return Task.CompletedTask;
        }

        private async Task AcceptTcpConnectionsAsync()
        {
            Log.Information("Starting TCP server on port {0}", Configuration.TcpPort);
            TcpListener = new TcpListener(IPAddress.Any, Configuration.TcpPort);
            TcpListener.Start();

            while (true)
            {
                try
                {
                    TcpClient tcpClient = await TcpListener.AcceptTcpClientAsync();

                    ACTcpClient acClient = new ACTcpClient(this, tcpClient);
                    acClient.HandshakeStarted += OnHandshakeStarted;
                    acClient.ChecksumFailed += OnClientChecksumFailed;
                    acClient.ChecksumPassed += OnClientChecksumPassed;
                    acClient.ChatMessageReceived += OnChatMessageReceived;
                    await acClient.StartAsync();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Something went wrong while trying to accept TCP connection.");
                }
            }
        }

        private void OnHandshakeStarted(ACTcpClient sender, ClientHandshakeEventArgs args)
        {
            ClientHandshakeStarted?.Invoke(sender, args);
        }

        private void OnClientChecksumPassed(ACTcpClient sender, EventArgs args)
        {
            ClientChecksumPassed?.Invoke(sender, args);
        }

        private void OnClientChecksumFailed(ACTcpClient sender, EventArgs args)
        {
            ClientChecksumFailed?.Invoke(sender, args);
        }

        private void OnChatMessageReceived(ACTcpClient sender, ChatMessageEventArgs args)
        {
            var client = (ACTcpClient)sender;
            
            Log.Information("CHAT: {0} ({1}): {2}", client.Name, client.SessionId, args.ChatMessage.Message);

            if (!CommandUtilities.HasPrefix(args.ChatMessage.Message, '/', out string commandStr))
            {
                var outArgs = new ChatEventArgs
                {
                    Message = args.ChatMessage.Message
                };
                ChatMessageReceived?.Invoke(sender, outArgs);

                if (!outArgs.Cancel)
                {
                    BroadcastPacket(args.ChatMessage);
                }
            }
            else
            {
                var message = args.ChatMessage;
                message.Message = commandStr;
                _ = ProcessCommandAsync(client, message);
            }
        }

        private void InitializeSteam()
        {
            if (Configuration.Extra.UseSteamAuth)
            {
                var serverInit = new SteamServerInit("assettocorsa", "Assetto Corsa")
                {
                    GamePort = (ushort)Configuration.UdpPort,
                    Secure = true,
                    QueryPort = 0xffff // MASTERSERVERUPDATERPORT_USEGAMESOCKETSHARE 
                };

                try
                {
                    SteamServer.Init(244210, serverInit);
                }
                catch { }

                try
                {
                    //SteamServer.Init(244210, serverInit);
                    SteamServer.LogOnAnonymous();
                    SteamServer.OnSteamServersDisconnected += SteamServer_OnSteamServersDisconnected;
                    SteamServer.OnSteamServersConnected += SteamServer_OnSteamServersConnected;
                }
                catch (Exception ex)
                {
                    Log.Fatal(ex, "Error trying to initialize SteamServer.");
                }
            }
        }

        private void SteamServer_OnSteamServersConnected()
        {
            Log.Information("Connected to Steam Servers.");
        }

        private void SteamServer_OnSteamServersDisconnected(Result obj)
        {
            Log.Fatal("Disconnected from Steam Servers.");
            SteamServer.OnSteamServersConnected -= SteamServer_OnSteamServersConnected;
            SteamServer.OnSteamServersDisconnected -= SteamServer_OnSteamServersDisconnected;

            try
            {
                SteamServer.LogOff();
            }
            catch { }

            try
            {
                SteamServer.Shutdown();
            }
            catch { }

            InitializeSteam();

        }
    }
}
