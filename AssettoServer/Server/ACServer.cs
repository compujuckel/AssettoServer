using System;
using System.IO;
using System.Net;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Reflection;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using App.Metrics;
using App.Metrics.AspNetCore;
using App.Metrics.Counter;
using App.Metrics.Formatters.Prometheus;
using App.Metrics.Timer;
using AssettoServer.Commands;
using AssettoServer.Network.Udp;
using AssettoServer.Network.Tcp;
using AssettoServer.Network.Http;
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
using Newtonsoft.Json;
using TimeZoneConverter;

namespace AssettoServer.Server
{
    public class ACServer
    {
        public ACServerConfiguration Configuration { get; }
        public CSPServerExtraOptions CSPServerExtraOptions { get; }
        public CSPLuaClientScriptProvider CSPLuaClientScriptProvider { get; }
        public int CurrentSessionIndex { get; private set; } = -1;
        public bool IsLastRaceInverted { get; private set; } = false;
        public bool MustInvertGrid { get; private set; } = false;
        [NotNull] public SessionState? CurrentSession { get; private set; }
        [NotNull] public WeatherData? CurrentWeather { get; private set; }
        public DateTime CurrentDateTime { get; set; }
        public TimeZoneInfo TimeZone { get; }
        public GeoParams GeoParams { get; private set; } = new GeoParams();
        public IReadOnlyList<string> Features { get; private set; }
        public IMetricsRoot Metrics { get; }
        public int StartTime { get; } = Environment.TickCount;
        public int CurrentTime => Environment.TickCount - StartTime;
        public long StartTime64 { get; } = Environment.TickCount64;
        public long CurrentTime64 => Environment.TickCount64 - StartTime64;

        internal ConcurrentDictionary<int, EntryCar> ConnectedCars { get; }
        internal ConcurrentDictionary<IPEndPoint, EntryCar> EndpointCars { get; }
        public EntryCar[] EntryCars { get; }
        internal GuidListFile Admins { get; }
        internal GuidListFile Blacklist { get; }
        [NotNull] internal ImmutableDictionary<string, byte[]>? TrackChecksums { get; private set; }
        [NotNull] internal ImmutableDictionary<string, byte[]>? CarChecksums { get; private set; }
        internal CommandService CommandService { get; }
        internal TcpListener? TcpListener { get; set; }
        internal ACUdpServer UdpServer { get; }
        internal IWebHost? HttpServer { get; private set; }
        internal KunosLobbyRegistration KunosLobbyRegistration { get; }
        internal Steam Steam { get; }
        internal Dictionary<int, CSPLuaMessageType> CSPLuaMessageTypes { get; } = new();

        internal SemaphoreSlim ConnectSemaphore { get; }
        private HttpClient HttpClient { get; }
        public IWeatherTypeProvider WeatherTypeProvider { get; }
        public DefaultWeatherProvider WeatherProvider { get; }
        public IWeatherImplementation WeatherImplementation { get; }
        private RainHelper RainHelper { get; }
        private ITrackParamsProvider TrackParamsProvider { get; }
        public TrackParams.TrackParams? TrackParams { get; }
        
        public TrafficMap? TrafficMap { get; }
        public AiBehavior? AiBehavior { get; }
        [MemberNotNullWhen(true, nameof(TrafficMap), nameof(AiBehavior))] public bool AiEnabled { get; }
        
        public ACPluginLoader PluginLoader { get; }
        
        private List<PosixSignalRegistration> SignalHandlers { get; }

        public event EventHandler<ACTcpClient, ClientHandshakeEventArgs>? ClientHandshakeStarted; 
        public event EventHandler<ACTcpClient, EventArgs>? ClientChecksumPassed;
        public event EventHandler<ACTcpClient, EventArgs>? ClientChecksumFailed;
        public event EventHandler<ACTcpClient, EventArgs>? ClientDisconnected;
        public event EventHandler<ACTcpClient, ClientAuditEventArgs>? ClientKicked;
        public event EventHandler<ACTcpClient, ClientAuditEventArgs>? ClientBanned;
        public event EventHandler<ACServer, EventArgs>? Update;
        public event EventHandler<ACTcpClient, ChatEventArgs>? ChatMessageReceived;
        
        public ACServer(ACServerConfiguration configuration, ACPluginLoader loader)
        {
            Log.Information("Starting server");

            Metrics = new MetricsBuilder()
                .Configuration.Configure(options => { options.DefaultContextLabel = "AssettoServer"; })
                .OutputMetrics.AsPrometheusPlainText()
                .OutputMetrics.AsPrometheusProtobuf()
                .Build();

            Configuration = configuration;
            EntryCars = Configuration.EntryCars.ToArray();
            Log.Information("Loaded {Count} cars", EntryCars.Length);
            for (int i = 0; i < EntryCars.Length; i++)
            {
                EntryCars[i].SessionId = (byte)i;
                EntryCars[i].Server = this;
                EntryCars[i].OtherCarsLastSentUpdateTime = new long[EntryCars.Length];
                EntryCars[i].AiInit();
            }

            CSPServerExtraOptions = new CSPServerExtraOptions(Configuration.WelcomeMessage);
            CSPServerExtraOptions.WelcomeMessage += LegalNotice.WelcomeMessage;
            CSPServerExtraOptions.ExtraOptions += "\r\n" + Configuration.CSPExtraOptions;
            CSPLuaClientScriptProvider = new CSPLuaClientScriptProvider(this);
            
            ConnectSemaphore = new SemaphoreSlim(1, 1);
            ConnectedCars = new ConcurrentDictionary<int, EntryCar>();
            EndpointCars = new ConcurrentDictionary<IPEndPoint, EntryCar>();
            Admins = new GuidListFile(this, "admins.txt");
            Blacklist = new GuidListFile(this, "blacklist.txt");
            Blacklist.Reloaded += OnBlacklistReloaded;
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

            if (Configuration.Extra.EnableClientMessages)
            {
                features.Add("CLIENT_MESSAGES");
                CSPClientMessageOutgoing.ChatEncoded = false;
            }

            features.Add("SPECTATING_AWARE");
            features.Add("LOWER_CLIENTS_SENDING_RATE");
            features.Add("CLIENTS_EXCHANGE_V1");

            Features = features;

            TrackParamsProvider = new IniTrackParamsProvider();
            TrackParamsProvider.Initialize().Wait();
            TrackParams = TrackParamsProvider.GetParamsForTrack(Configuration.Track);

            if (TrackParams == null)
            {
                if (Configuration.Extra.IgnoreConfigurationErrors.MissingTrackParams)
                {
                    Log.Warning("Using UTC as default time zone");
                    TimeZone = TimeZoneInfo.Utc;
                }
                else
                {
                    throw new ConfigurationException($"No track params found for {Configuration.Track}. More info: https://github.com/compujuckel/AssettoServer/wiki/Common-configuration-errors#missing-track-params");
                }
            }
            else if (string.IsNullOrEmpty(TrackParams.Timezone))
            {
                if (Configuration.Extra.IgnoreConfigurationErrors.MissingTrackParams)
                {
                    Log.Warning("Using UTC as default time zone");
                    TimeZone = TimeZoneInfo.Utc;
                }
                else
                {
                    throw new ConfigurationException($"No time zone found for {Configuration.Track}. More info: https://github.com/compujuckel/AssettoServer/wiki/Common-configuration-errors#missing-track-params");
                }
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

            AiEnabled = Configuration.Extra.EnableAi;

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


            Steam = new Steam(this);
            if (Configuration.Extra.UseSteamAuth)
            {
                Steam.Initialize();
            }
        }

        public void NextSession()
        {
            // TODO StallSessionSwitch
            // TODO reset sun angle

            if (Configuration.Sessions.Count - 1 == CurrentSessionIndex)
            {
                if (Configuration.Loop)
                {
                    Log.Information("Looping sessions");
                }
                else if(CurrentSession.Configuration.Type != SessionType.Race || Configuration.InvertedGridPositions == 0 || IsLastRaceInverted)
                {
                    // TODO exit
                }

                if (CurrentSession.Configuration.Type == SessionType.Race && Configuration.InvertedGridPositions != 0)
                {
                    if (Configuration.Sessions.Count <= 1)
                    {
                        MustInvertGrid = true;
                    }
                    else if (!IsLastRaceInverted)
                    {
                        MustInvertGrid = true;
                        IsLastRaceInverted = true;
                        --CurrentSessionIndex;
                    }
                }
            }

            if (++CurrentSessionIndex >= Configuration.Sessions.Count)
            {
                CurrentSessionIndex = 0;
            }

            var previousSessionResults = CurrentSession?.Results;
            
            CurrentSession = new SessionState(Configuration.Sessions[CurrentSessionIndex])
            {
                Results = new Dictionary<byte, EntryCarResult>(),
                StartTime = DateTime.Now,
                StartTimeTicks = CurrentTime
            };
            
            foreach (var entryCar in EntryCars)
            {
                CurrentSession.Results.Add(entryCar.SessionId, new EntryCarResult());
            }
            
            Log.Information("Next session: {SessionName}", CurrentSession.Configuration.Name);

            if (CurrentSession.Configuration.Type == SessionType.Race)
            {
                CurrentSession.StartTimeTicks = CurrentTime + (CurrentSession.Configuration.WaitTime * 1000);
            }
            else
            {
                IsLastRaceInverted = false;
            }
            
            // TODO dynamic track
            // TODO weather
            // TODO reset mandatory pits and P2P count

            if (previousSessionResults == null)
            {
                CurrentSession.Grid = EntryCars;
            }
            else
            {
                CurrentSession.Grid = previousSessionResults
                    .OrderBy(result => result.Value.BestLap)
                    .Select(result => EntryCars[result.Key]);
            }
            
            SendCurrentSession();
        }
        
        internal void SendCurrentSession(ACTcpClient? target = null)
        {
            var packet = new CurrentSessionUpdate
            {
                CurrentSession = CurrentSession.Configuration,
                Grid = CurrentSession.Grid,
                TrackGrip = Math.Clamp(Configuration.DynamicTrack.Enabled ? Configuration.DynamicTrack.BaseGrip + (Configuration.DynamicTrack.GripPerLap * Configuration.DynamicTrack.TotalLapCount) : 1, 0, 1),
            };

            if (target == null)
            {
                foreach (var car in EntryCars.Where(c => c.Client != null && c.Client.HasSentFirstUpdate))
                {
                    packet.StartTime = CurrentSession.StartTimeTicks - car.TimeOffset;
                    car.Client?.SendPacket(packet);
                }
            }
            else
            {
                target.SendPacket(packet);
            }
        }

        public bool IsSessionOver()
        {
            if (CurrentSession.Configuration.Type != SessionType.Race)
            {
                return (CurrentTime - CurrentSession.StartTimeTicks) > 60_000 * CurrentSession.Configuration.Time;
            }

            return false;
        }

        public async Task StartAsync()
        {
            NextSession();

            await Blacklist.LoadAsync();
            await Admins.LoadAsync();

            if (!Configuration.Extra.UseSteamAuth && Admins.List.Any())
            {
                const string errorMsg =
                    "Admin whitelist is enabled but Steam auth is disabled. This is unsafe because it allows players to gain admin rights by SteamID spoofing. More info: https://github.com/compujuckel/AssettoServer/wiki/Common-configuration-errors#unsafe-admin-whitelist";
                if (Configuration.Extra.IgnoreConfigurationErrors.UnsafeAdminWhitelist)
                {
                    Log.Warning(errorMsg);
                }
                else
                {
                    throw new ConfigurationException(errorMsg);
                }
            }
            
            await InitializeGeoParams();
            
            _ = Task.Factory.StartNew(AcceptTcpConnectionsAsync, TaskCreationOptions.LongRunning);
            UdpServer.Start();
            
            for (var i = 0; i < EntryCars.Length; i++)
            {
                EntryCars[i].ResetLogger();
            }

            Log.Information("Starting HTTP server on port {HttpPort}", Configuration.HttpPort);
            
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
            
            foreach (var plugin in PluginLoader.LoadedPlugins)
            {
                plugin.Instance.Initialize(this);
            }
        }

        private async Task InitializeGeoParams()
        {
            try
            {
                HttpResponseMessage response = await HttpClient.GetAsync("http://ip-api.com/json");

                if (response.IsSuccessStatusCode)
                {
                    string jsonString = await response.Content.ReadAsStringAsync();
                    Dictionary<string, string> json = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonString) ?? throw new JsonException("Cannot deserialize ip-api.com response");
                    GeoParams = new GeoParams
                    {
                        Ip = json["query"],
                        City = json["city"],
                        Country = json["country"],
                        CountryCode = json["countryCode"]
                    };
                    
                    Log.Information("Server invite link: {ServerInviteLink}", $"https://acstuff.ru/s/q:race/online/join?ip={GeoParams.Ip}&httpPort={Configuration.HttpPort}");
                }
                else
                {
                    Log.Error("Failed to get IP geolocation parameters");
                }
            }
            catch (HttpRequestException e)
            {
                Log.Error(e, "Failed to get IP geolocation parameters");
            }

            if (Configuration.Extra.GeoParamsCountryOverride != null)
            {
                GeoParams.City = "";
                GeoParams.Country = Configuration.Extra.GeoParamsCountryOverride[0];
                GeoParams.CountryCode = Configuration.Extra.GeoParamsCountryOverride[1];
            }
        }

        private void TerminateHandler(PosixSignalContext context)
        {
            Log.Information("Caught signal, server shutting down");
            BroadcastPacket(new ChatMessage { SessionId = 255, Message = "*** Server shutting down ***" });
                
            // Allow some time for the chat messages to be sent
            Thread.Sleep(250);
            Log.CloseAndFlush();
        }
        
        private void ReloadHandler(PosixSignalContext context)
        {
            Log.Information("Reloading blacklist and adminlist...");

            _ = Blacklist.LoadAsync();
            _ = Admins.LoadAsync();
            
            context.Cancel = true;
        }

        private void OnBlacklistReloaded(GuidListFile sender, EventArgs e)
        {
            foreach (var client in ConnectedCars.Values.Select(c => c.Client))
            {
                if (client != null && client.Guid != null && Blacklist.Contains(client.Guid))
                {
                    client.Logger.Information("{ClientName} was banned after reloading blacklist", client.Name);
                    client.SendPacket(new KickCar {SessionId = client.SessionId, Reason = KickReason.VoteBlacklisted});
                    
                    _ = client.DisconnectAsync();
                }
            }
        }
        
        private void InitializeChecksums()
        {
            TrackChecksums = ChecksumsProvider.CalculateTrackChecksums(Configuration.Track, Configuration.TrackConfig);
            Log.Information("Initialized {Count} track checksums", TrackChecksums.Count);

            var carModels = EntryCars.Select(car => car.Model).Distinct().ToList();
            CarChecksums = ChecksumsProvider.CalculateCarChecksums(carModels);
            Log.Information("Initialized {Count} car checksums", CarChecksums.Count);

            var modelsWithoutChecksums = carModels.Except(CarChecksums.Keys).ToList();
            if (modelsWithoutChecksums.Count > 0)
            {
                string models = string.Join(", ", modelsWithoutChecksums);

                if (Configuration.Extra.IgnoreConfigurationErrors.MissingCarChecksums)
                {
                    Log.Warning("No data.acd found for {CarModels}. This will allow players to cheat using modified data. More info: https://github.com/compujuckel/AssettoServer/wiki/Common-configuration-errors#missing-car-checksums", models);
                }
                else
                {
                    throw new ConfigurationException($"No data.acd found for {models}. This will allow players to cheat using modified data. More info: https://github.com/compujuckel/AssettoServer/wiki/Common-configuration-errors#missing-car-checksums");
                }
            }
        }

        public bool IsGuidBlacklisted(string guid)
        {
            return Blacklist.Contains(guid);
        }
        
        public async Task<bool> TrySecureSlotAsync(ACTcpClient client, HandshakeRequest handshakeRequest)
        {
            try
            {
                await ConnectSemaphore.WaitAsync();

                if (ConnectedCars.Count >= Configuration.MaxClients)
                    return false;

                for (int i = 0; i < EntryCars.Length; i++)
                {
                    EntryCar entryCar = EntryCars[i];
                    if (entryCar.Client != null && entryCar.Client.Guid == client.Guid)
                        return false;

                    var isAdmin = !string.IsNullOrEmpty(handshakeRequest.Guid) && Admins.Contains(handshakeRequest.Guid);
                    
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
                client.Logger.Error(ex, "Error securing slot for {ClientName}", client.Name);
            }
            finally
            {
                ConnectSemaphore.Release();
            }

            return false;
        }

        public async Task KickAsync(ACTcpClient? client, KickReason reason, string? reasonStr = null, bool broadcastMessage = true, ACTcpClient? admin = null)
        {
            if (client != null && !client.IsDisconnectRequested)
            {
                if (reasonStr != null && broadcastMessage)
                    BroadcastPacket(new ChatMessage {SessionId = 255, Message = reasonStr});
                
                client.Logger.Information("{ClientName} was kicked. Reason: {Reason}", client.Name, reasonStr ?? "No reason given.");
                client.SendPacket(new KickCar {SessionId = client.SessionId, Reason = reason});

                var args = new ClientAuditEventArgs
                {
                    Reason = reason,
                    ReasonStr = reasonStr,
                    Admin = admin
                };
                ClientKicked?.Invoke(client, args);
                
                await client.DisconnectAsync();
            }
        }

        public async Task BanAsync(ACTcpClient? client, KickReason reason, string? reasonStr = null, ACTcpClient? admin = null)
        {
            if (client != null && client.Guid != null && !client.IsDisconnectRequested)
            {
                if (reasonStr != null)
                    BroadcastPacket(new ChatMessage {SessionId = 255, Message = reasonStr});
                
                client.Logger.Information("{ClientName} was banned. Reason: {Reason}", client.Name, reasonStr ?? "No reason given.");
                client.SendPacket(new KickCar {SessionId = client.SessionId, Reason = reason});
                
                var args = new ClientAuditEventArgs
                {
                    Reason = reason,
                    ReasonStr = reasonStr,
                    Admin = admin
                };
                ClientBanned?.Invoke(client, args);
                
                await client.DisconnectAsync();
                await Blacklist.AddAsync(client.Guid);
            }
        }
        
        public void SetWeather(WeatherData weather)
        {
            CurrentWeather = weather;
            WeatherImplementation.SendWeather();
        }
        
        public void SetCspWeather(WeatherFxType upcoming, int duration)
        {
            Log.Information("CSP weather transitioning to {UpcomingWeatherType}", upcoming);
            
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
        
        public void SendLapCompletedMessage(byte sessionId, int lapTime, int cuts, ACTcpClient? target = null)
        {
            if (CurrentSession.Results == null)
                throw new ArgumentNullException(nameof(CurrentSession.Results));
            
            var laps = CurrentSession.Results
                .Select((result) => new LapCompletedOutgoing.CompletedLap
                {
                    SessionId = result.Key,
                    LapTime = CurrentSession.Configuration.Type == SessionType.Race ? result.Value.TotalTime : result.Value.BestLap,
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
                TrackGrip = CurrentWeather.TrackGrip
            };

            if (target == null)
            {
                BroadcastPacket(packet);
            }
            else
            {
                target.SendPacket(packet);
            }
        }

        public void BroadcastPacket<TPacket>(TPacket packet, ACTcpClient? sender = null) where TPacket : IOutgoingNetworkPacket
        {
            if (!(packet is SunAngleUpdate))
                Log.Verbose("Broadcasting {PacketName}", typeof(TPacket).Name);

            foreach (EntryCar car in EntryCars.Where(c => c.Client != null && c.Client.HasSentFirstUpdate && sender != c.Client))
                car.Client?.SendPacket(packet);
        }
        
        public void BroadcastPacketUdp<TPacket>(TPacket packet, ACTcpClient? sender = null) where TPacket : IOutgoingNetworkPacket
        {
            if (!(packet is SunAngleUpdate))
                Log.Verbose("Broadcasting {PacketName}", typeof(TPacket).Name);

            foreach (EntryCar car in EntryCars.Where(c => c.Client != null && c.Client.HasSentFirstUpdate && sender != c.Client && c.Client.HasAssociatedUdp))
                car.Client?.SendPacketUdp(packet);
        }

        [SuppressMessage("ReSharper", "FunctionNeverReturns")]
        private async Task UpdateAsync()
        {
            int sleepMs = 1000 / Configuration.RefreshRateHz;
            long nextTick = Environment.TickCount64;
            long lastTimeUpdate = Environment.TickCount64;
            Dictionary<EntryCar, CountedArray<PositionUpdateOut>> positionUpdates = new();
            foreach (var entryCar in EntryCars)
            {
                positionUpdates[entryCar] = new CountedArray<PositionUpdateOut>(EntryCars.Length);
            }

            Log.Information("Starting update loop with an update rate of {RefreshRateHz}hz", Configuration.RefreshRateHz);

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
            Metrics.Measure.Counter.Increment(updateLoopLateOptions, 0);
            
            while (true)
            {
                try
                {
                    using (Metrics.Measure.Timer.Time(timerOptions))
                    {
                        Update?.Invoke(this, EventArgs.Empty);

                        for (int i = 0; i < EntryCars.Length; i++)
                        {
                            var fromCar = EntryCars[i];
                            var fromClient = fromCar.Client;
                            if (fromClient != null && fromClient.HasSentFirstUpdate && (CurrentTime - fromCar.LastPingTime) > 1000)
                            {
                                fromCar.CheckAfk();
                                fromCar.LastPingTime = CurrentTime;
                                fromClient.SendPacketUdp(new PingUpdate(CurrentTime, fromCar.Ping));

                                if (CurrentTime - fromCar.LastPongTime > 15000)
                                {
                                    fromClient.Logger.Information("{ClientName} has not sent a ping response for over 15 seconds", fromClient.Name);
                                    _ = fromClient.DisconnectAsync();
                                }
                            }

                            if (fromCar.AiControlled || fromCar.HasUpdateToSend)
                            {
                                fromCar.HasUpdateToSend = false;

                                for (int j = 0; j < EntryCars.Length; j++)
                                {
                                    var toCar = EntryCars[j];
                                    var toClient = toCar.Client;
                                    if (toCar == fromCar || toClient == null || !toClient.HasSentFirstUpdate
                                        || !fromCar.GetPositionUpdateForCar(toCar, out var update)) continue;

                                    if (Configuration.Extra.BatchedPositionUpdateBehavior == BatchedPositionUpdateBehavior.Full
                                        || (fromCar.AiControlled && Configuration.Extra.BatchedPositionUpdateBehavior == BatchedPositionUpdateBehavior.AiOnly))
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

                        if (Configuration.Extra.BatchedPositionUpdateBehavior != BatchedPositionUpdateBehavior.None)
                        {
                            foreach (var (toCar, updates) in positionUpdates)
                            {
                                if (updates.Count == 0) continue;
                                
                                var toClient = toCar.Client;
                                if (toClient != null)
                                {
                                    const int chunkSize = 20;
                                    for (int i = 0; i < updates.Count; i += chunkSize)
                                    {
                                        var batchedUpdate = new BatchedPositionUpdate((uint)(CurrentTime - toCar.TimeOffset), toCar.Ping,
                                            new ArraySegment<PositionUpdateOut>(updates.Array, i, Math.Min(i + chunkSize, updates.Count)));
                                        toClient.SendPacketUdp(in batchedUpdate);
                                    }
                                }
                                
                                updates.Clear();
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

                        if (IsSessionOver())
                        {
                            NextSession();
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
                                    Log.Warning("Server is running {TickDelta}ms behind", -tickDelta);

                                Metrics.Measure.Counter.Increment(updateLoopLateOptions, -tickDelta);
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
                    Log.Error(ex, "Something went wrong while trying to do a tick update");
                }
            }
        }

        public void RegisterCspLuaMessageType(ushort type, Func<IIncomingNetworkPacket> factoryMethod, Action<ACTcpClient, IIncomingNetworkPacket> handler)
        {
            if (CSPLuaMessageTypes.ContainsKey(type))
                throw new ArgumentException($"Type {type} already registered");

            CSPLuaMessageTypes.Add(type, new CSPLuaMessageType(type, factoryMethod, handler));
        }

        internal async Task DisconnectClientAsync(ACTcpClient client)
        {
            try
            {
                await ConnectSemaphore.WaitAsync();
                if (client.IsConnected && client.EntryCar.Client == client && ConnectedCars.TryRemove(client.SessionId, out _))
                {
                    client.Logger.Information("{ClientName} has disconnected", client.Name);

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
                client.Logger.Error(ex, "Error disconnecting {ClientName}", client.Name);
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
                (e.Context as ACCommandContext)?.Reply("An error occurred while executing this command.");
                Log.Error(e.Result.Exception, "Command execution failed: {Reason}", e.Result.FailureReason);
            }

            return Task.CompletedTask;
        }

        [SuppressMessage("ReSharper", "FunctionNeverReturns")]
        private async Task AcceptTcpConnectionsAsync()
        {
            Log.Information("Starting TCP server on port {TcpPort}", Configuration.TcpPort);
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
                    Log.Error(ex, "Something went wrong while trying to accept TCP connection");
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
            if (!CommandUtilities.HasPrefix(args.ChatMessage.Message, '/', out string commandStr))
            {
                var outArgs = new ChatEventArgs(args.ChatMessage.Message);
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
                _ = ProcessCommandAsync(sender, message);
            }
        }
    }
}
