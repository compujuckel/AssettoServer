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
using AssettoServer.Network.Packets;
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
using NanoSockets;
using Serilog;
using Qmmands;
using Newtonsoft.Json;
using NodaTime;
using NodaTime.Extensions;
using SunCalcNet;
using SunCalcNet.Model;

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
        public ZonedDateTime CurrentDateTime { get; set; }
        public SunPosition? CurrentSunPosition { get; private set; }
        public GeoParams GeoParams { get; private set; } = new GeoParams();
        public IReadOnlyList<string> Features { get; private set; }
        public IMetricsRoot Metrics { get; }
        public int StartTime { get; private set; }
        public int CurrentTime => Environment.TickCount - StartTime;
        public long StartTime64 { get; private set; }
        public long CurrentTime64 => Environment.TickCount64 - StartTime64;

        internal ConcurrentDictionary<int, EntryCar> ConnectedCars { get; }
        internal ConcurrentDictionary<Address, EntryCar> EndpointCars { get; }
        public EntryCar[] EntryCars { get; }
        internal GuidListFile Admins { get; }
        internal GuidListFile Blacklist { get; }
        [NotNull] internal ImmutableDictionary<string, byte[]>? TrackChecksums { get; private set; }
        [NotNull] internal ImmutableDictionary<string, byte[]>? CarChecksums { get; private set; }
        internal CommandService CommandService { get; }
        internal TcpListener? TcpListener { get; set; }
        internal ACUdpServerNano UdpServer { get; }
        internal IWebHost? HttpServer { get; private set; }
        internal KunosLobbyRegistration KunosLobbyRegistration { get; }
        internal Steam Steam { get; }
        internal Dictionary<uint, Action<ACTcpClient, PacketReader>> CSPClientMessageTypes { get; } = new();

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

        /// <summary>
        /// Fires when a client has started a handshake. At this point it is still possible to reject the connection by setting ClientHandshakeEventArgs.Cancel = true.
        /// </summary>
        public event EventHandler<ACTcpClient, ClientHandshakeEventArgs>? ClientHandshakeStarted;
        
        /// <summary>
        /// Fires when a client passed the checksum checks. This does not mean that the player has finished loading, use ClientFirstUpdateSent for that.
        /// </summary>
        public event EventHandler<ACTcpClient, EventArgs>? ClientChecksumPassed;
        
        /// <summary>
        /// Fires when a client failed the checksum check.
        /// </summary>
        public event EventHandler<ACTcpClient, EventArgs>? ClientChecksumFailed;
        
        /// <summary>
        /// Fires when a player has disconnected.
        /// </summary>
        public event EventHandler<ACTcpClient, EventArgs>? ClientDisconnected;
        
        /// <summary>
        /// Fires when a client has sent the first position update and is visible to other players.
        /// </summary>
        public event EventHandler<ACTcpClient, EventArgs>? ClientFirstUpdateSent;
        
        /// <summary>
        /// Fires when a client has been kicked.
        /// </summary>
        public event EventHandler<ACTcpClient, ClientAuditEventArgs>? ClientKicked;
        
        /// <summary>
        /// Fires when a client has been banned.
        /// </summary>
        public event EventHandler<ACTcpClient, ClientAuditEventArgs>? ClientBanned;

        /// <summary>
        /// Fires when a client collided with something. TargetCar will be null for environment collisions.
        /// There are up to 5 seconds delay before a collision is reported to the server.
        /// </summary>
        public event EventHandler<ACTcpClient, CollisionEventArgs>? ClientCollision; 

        /// <summary>
        /// Fires on each server tick in the main loop. Don't do resource intensive / long running stuff in here!
        /// </summary>
        public event EventHandler<ACServer, EventArgs>? Update;
        
        /// <summary>
        /// Fires when a client has sent a chat message. Set ChatEventArgs.Cancel = true to stop it from being broadcast to other players.
        /// </summary>
        public event EventHandler<ACTcpClient, ChatEventArgs>? ChatMessageReceived;

        /// <summary>
        /// Fires when a new session is started
        /// </summary>
        public event EventHandler<ACServer, SessionChangedEventArgs>? SessionChanged; 

        public ACServer(ACServerConfiguration configuration, ACPluginLoader loader)
        {
            Log.Information("Starting server");

            Metrics = new MetricsBuilder()
                .Configuration.Configure(options => { options.DefaultContextLabel = "AssettoServer"; })
                .OutputMetrics.AsPrometheusPlainText()
                .OutputMetrics.AsPrometheusProtobuf()
                .Build();

            Configuration = configuration;
            CSPServerExtraOptions = new CSPServerExtraOptions(Configuration.WelcomeMessage);
            CSPServerExtraOptions.WelcomeMessage += LegalNotice.WelcomeMessage;
            if (Configuration.Extra.EnableCustomUpdate)
            {
                CSPServerExtraOptions.ExtraOptions += "\r\n" + $"[EXTRA_DATA]\r\nCUSTOM_UPDATE_FORMAT = '{CSPPositionUpdate.CustomUpdateFormat}'";
            }
            CSPServerExtraOptions.ExtraOptions += "\r\n" + Configuration.CSPExtraOptions;
            CSPLuaClientScriptProvider = new CSPLuaClientScriptProvider(this);
            
            ConnectSemaphore = new SemaphoreSlim(1, 1);
            ConnectedCars = new ConcurrentDictionary<int, EntryCar>();
            EndpointCars = new ConcurrentDictionary<Address, EntryCar>();
            Admins = new GuidListFile(this, "admins.txt");
            Blacklist = new GuidListFile(this, "blacklist.txt");
            Blacklist.Reloaded += OnBlacklistReloaded;
            UdpServer = new ACUdpServerNano(this, Configuration.Server.UdpPort);
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
            
            if (Configuration.Extra.EnableCustomUpdate)
                features.Add("CUSTOM_UPDATE");

            features.Add("SPECTATING_AWARE");
            features.Add("LOWER_CLIENTS_SENDING_RATE");

            Features = features;

            TrackParamsProvider = new IniTrackParamsProvider();
            TrackParamsProvider.Initialize().Wait();
            TrackParams = TrackParamsProvider.GetParamsForTrack(Configuration.Server.Track);

            DateTimeZone? timeZone;
            if (TrackParams == null)
            {
                if (Configuration.Extra.IgnoreConfigurationErrors.MissingTrackParams)
                {
                    Log.Warning("Using UTC as default time zone");
                    timeZone = DateTimeZone.Utc;
                }
                else
                {
                    throw new ConfigurationException($"No track params found for {Configuration.Server.Track}. More info: https://github.com/compujuckel/AssettoServer/wiki/Common-configuration-errors#missing-track-params");
                }
            }
            else if (string.IsNullOrEmpty(TrackParams.Timezone))
            {
                if (Configuration.Extra.IgnoreConfigurationErrors.MissingTrackParams)
                {
                    Log.Warning("Using UTC as default time zone");
                    timeZone = DateTimeZone.Utc;
                }
                else
                {
                    throw new ConfigurationException($"No time zone found for {Configuration.Server.Track}. More info: https://github.com/compujuckel/AssettoServer/wiki/Common-configuration-errors#missing-track-params");
                }
            }
            else
            {
                timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull(TrackParams.Timezone);

                if (timeZone == null)
                {
                    throw new ConfigurationException($"Invalid time zone {TrackParams.Timezone} for track {Configuration.Server.Track}. Please enter a valid time zone for your track in cfg/data_track_params.ini.");
                }
            }
            
            CurrentDateTime = SystemClock.Instance.InZone(timeZone).GetCurrentDate().AtStartOfDayInZone(timeZone).PlusSeconds((long)WeatherUtils.SecondsFromSunAngle(Configuration.Server.SunAngle));
            
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

            if (Configuration.Extra.EnableAi)
            {
                string mapAiBasePath = "content/tracks/" + Configuration.Server.Track + "/ai/";
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
                    AiEnabled = false;
                }
                else
                {
                    AiBehavior = new AiBehavior(this);
                    AiEnabled = true;
                }
            }
            
            EntryCars = new EntryCar[Math.Min(Configuration.Server.MaxClients, Configuration.EntryList.Cars.Count)];
            Log.Information("Loaded {Count} cars", EntryCars.Length);
            for (int i = 0; i < EntryCars.Length; i++)
            {
                var entry = Configuration.EntryList.Cars[i];
                var driverOptions = CSPDriverOptions.Parse(entry.Skin);
                var aiMode = AiEnabled ? entry.AiMode : AiMode.None;
                
                EntryCars[i] = new EntryCar(entry.Model, entry.Skin, this, (byte)i)
                {
                    SpectatorMode = entry.SpectatorMode,
                    Ballast = entry.Ballast,
                    Restrictor = entry.Restrictor,
                    DriverOptionsFlags = driverOptions,
                    AiMode = aiMode,
                    AiEnableColorChanges = driverOptions.HasFlag(DriverOptionsFlags.AllowColorChange),
                    AiControlled = aiMode != AiMode.None,
                    NetworkDistanceSquared = MathF.Pow(Configuration.Extra.NetworkBubbleDistance, 2),
                    OutsideNetworkBubbleUpdateRateMs = 1000 / Configuration.Extra.OutsideNetworkBubbleRefreshRateHz
                };
            }
            
            WeatherProvider = new DefaultWeatherProvider(this);

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
                if (Configuration.Server.Loop)
                {
                    Log.Information("Looping sessions");
                }
                else if(CurrentSession.Configuration.Type != SessionType.Race || Configuration.Server.InvertedGridPositions == 0 || IsLastRaceInverted)
                {
                    // TODO exit
                }

                if (CurrentSession.Configuration.Type == SessionType.Race && Configuration.Server.InvertedGridPositions != 0)
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

            var previousSession = CurrentSession;
            var previousSessionResults = CurrentSession?.Results;
            
            CurrentSession = new SessionState(Configuration.Sessions[CurrentSessionIndex], this)
            {
                Results = new Dictionary<byte, EntryCarResult>(),
                StartTimeTicks64 = CurrentTime64
            };
            
            foreach (var entryCar in EntryCars)
            {
                CurrentSession.Results.Add(entryCar.SessionId, new EntryCarResult());
            }
            
            Log.Information("Next session: {SessionName}. Start time (ingame): {StartTime}", CurrentSession.Configuration.Name, CurrentDateTime);

            if (CurrentSession.Configuration.Type == SessionType.Race)
            {
                CurrentSession.StartTimeTicks64 = CurrentTime64 + (CurrentSession.Configuration.WaitTime * 1000);
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
            
            SessionChanged?.Invoke(this, new SessionChangedEventArgs(previousSession, CurrentSession));
            SendCurrentSession();
        }
        
        internal void SendCurrentSession(ACTcpClient? target = null)
        {
            var packet = new CurrentSessionUpdate
            {
                CurrentSession = CurrentSession.Configuration,
                Grid = CurrentSession.Grid,
                TrackGrip = Math.Clamp(Configuration.Server.DynamicTrack != null ? Configuration.Server.DynamicTrack.BaseGrip + (Configuration.Server.DynamicTrack.GripPerLap * Configuration.Server.DynamicTrack.TotalLapCount) : 1, 0, 1),
            };

            if (target == null)
            {
                foreach (var car in EntryCars.Where(c => c.Client != null && c.Client.HasSentFirstUpdate))
                {
                    packet.StartTime = CurrentSession.StartTimeTicks64 - car.TimeOffset;
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
                return (CurrentTime - CurrentSession.StartTimeTicks64) > 60_000 * CurrentSession.Configuration.Time;
            }

            return false;
        }

        public async Task StartAsync()
        {
            StartTime = Environment.TickCount;
            StartTime64 = Environment.TickCount64;
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

            Log.Information("Starting HTTP server on port {HttpPort}", Configuration.Server.HttpPort);
            
            HttpServer = WebHost.CreateDefaultBuilder()
                .ConfigureKestrel(options => options.AllowSynchronousIO = true)
                .ConfigureMetrics(Metrics)
                .UseMetrics(options => { options.EndpointOptions = endpointsOptions => { endpointsOptions.MetricsEndpointOutputFormatter = Metrics.OutputMetricsFormatters.OfType<MetricsPrometheusTextOutputFormatter>().First(); }; })
                .UseSerilog()
                .UseStartup(_ => new Startup(this))
                .UseUrls($"http://0.0.0.0:{Configuration.Server.HttpPort}")
                .Build();
            await HttpServer.StartAsync();
            
            if (Configuration.Server.RegisterToLobby)
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
                    
                    Log.Information("Server invite link: {ServerInviteLink}", $"https://acstuff.ru/s/q:race/online/join?ip={GeoParams.Ip}&httpPort={Configuration.Server.HttpPort}");
                }
                else
                {
                    Log.Error("Failed to get IP geolocation parameters");
                }
            }
            catch (Exception e)
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
            TrackChecksums = ChecksumsProvider.CalculateTrackChecksums(Configuration.Server.Track, Configuration.Server.TrackConfig);
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

                if (ConnectedCars.Count >= Configuration.Server.MaxClients)
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
            CurrentDateTime = CurrentDateTime.Date.AtStartOfDayInZone(CurrentDateTime.Zone).PlusSeconds((long)time);
            UpdateSunPosition();
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
            for (int i = 0; i < EntryCars.Length; i++)
            {
                var car = EntryCars[i];
                if (car.Client is { HasSentFirstUpdate: true } && car.Client != sender)
                {
                    car.Client?.SendPacket(packet);
                }
            }
        }
        
        public void BroadcastPacketUdp<TPacket>(TPacket packet, ACTcpClient? sender = null) where TPacket : IOutgoingNetworkPacket
        {
            for (int i = 0; i < EntryCars.Length; i++)
            {
                var car = EntryCars[i];
                if (car.Client is { HasSentFirstUpdate: true, HasAssociatedUdp: true } && car.Client != sender)
                {
                    car.Client?.SendPacketUdp(in packet);
                }
            }
        }

        [SuppressMessage("ReSharper", "FunctionNeverReturns")]
        private async Task UpdateAsync()
        {
            int failedUpdateLoops = 0;
            int sleepMs = 1000 / Configuration.Server.RefreshRateHz;
            long nextTick = Environment.TickCount64;
            long lastTimeUpdate = Environment.TickCount64;
            Dictionary<EntryCar, CountedArray<PositionUpdateOut>> positionUpdates = new();
            foreach (var entryCar in EntryCars)
            {
                positionUpdates[entryCar] = new CountedArray<PositionUpdateOut>(EntryCars.Length);
            }

            Log.Information("Starting update loop with an update rate of {RefreshRateHz}hz", Configuration.Server.RefreshRateHz);

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
                                        var packet = new BatchedPositionUpdate((uint)(CurrentTime - toCar.TimeOffset), toCar.Ping,
                                            new ArraySegment<PositionUpdateOut>(updates.Array, i, Math.Min(chunkSize, updates.Count - i)));
                                        toClient.SendPacketUdp(in packet);
                                    }
                                }
                            }
                            
                            updates.Clear();
                        }

                        if (Environment.TickCount64 - lastTimeUpdate > 1000)
                        {
                            if (Configuration.Extra.EnableRealTime)
                            {
                                CurrentDateTime = SystemClock.Instance.InZone(CurrentDateTime.Zone).GetCurrentZonedDateTime();
                            }
                            else
                            {
                                CurrentDateTime += Duration.FromMilliseconds((Environment.TickCount64 - lastTimeUpdate) * Configuration.Server.TimeOfDayMultiplier);
                            }
                            
                            UpdateSunPosition();

                            RainHelper.Update(CurrentWeather, Configuration.Server.DynamicTrack?.BaseGrip ?? 1, Configuration.Extra.RainTrackGripReductionPercent, Environment.TickCount64 - lastTimeUpdate);
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

        public void UpdateSunPosition()
        {
            if (TrackParams != null)
            {
                CurrentSunPosition = SunCalc.GetSunPosition(CurrentDateTime.ToDateTimeUtc(), TrackParams.Latitude, TrackParams.Longitude);
            }
        }

        public void RegisterCSPClientMessageType(uint type, Action<ACTcpClient, PacketReader> handler)
        {
            if (CSPClientMessageTypes.ContainsKey(type))
                throw new ArgumentException($"Type {type} already registered");

            CSPClientMessageTypes.Add(type, handler);
        }

        internal async Task DisconnectClientAsync(ACTcpClient client)
        {
            try
            {
                await ConnectSemaphore.WaitAsync();
                if (client.IsConnected && client.EntryCar.Client == client && ConnectedCars.TryRemove(client.SessionId, out _))
                {
                    client.Logger.Information("{ClientName} has disconnected", client.Name);

                    if (client.UdpEndpoint.HasValue)
                        EndpointCars.TryRemove(client.UdpEndpoint.Value, out _);

                    client.EntryCar.Client = null;
                    client.IsConnected = false;

                    if (client.HasPassedChecksum)
                        BroadcastPacket(new CarDisconnected { SessionId = client.SessionId });
                    
                    client.EntryCar.Reset();
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
            Log.Information("Starting TCP server on port {TcpPort}", Configuration.Server.TcpPort);
            TcpListener = new TcpListener(IPAddress.Any, Configuration.Server.TcpPort);
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
                    acClient.FirstUpdateSent += OnClientFirstUpdateSent;
                    acClient.Collision += OnCollision;
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

        private void OnClientFirstUpdateSent(ACTcpClient sender, EventArgs args)
        {
            ClientFirstUpdateSent?.Invoke(sender, args);
        }

        private void OnCollision(ACTcpClient sender, CollisionEventArgs args)
        {
            ClientCollision?.Invoke(sender, args);
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
