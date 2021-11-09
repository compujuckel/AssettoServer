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
using AssettoServer.Server.TrackParams;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Serilog;
using Serilog.Core;
using Qmmands;
using Steamworks;
using Newtonsoft.Json;
using TimeZoneConverter;

namespace AssettoServer.Server
{
    public class ACServer
    {
        public ILogger ChatLog { get; }
        public ACServerConfiguration Configuration { get; }
        public SessionConfiguration CurrentSession { get; private set; }
        public WeatherData CurrentWeather { get; private set; }
        public long? WeatherFxStartDate { get; private set; }
        public float CurrentDaySeconds { get; private set; }
        public GeoParams GeoParams { get; private set; }
        public IReadOnlyList<string> Features { get; private set; }
        public Discord Discord { get; }

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

        internal int StartTime { get; } = Environment.TickCount;
        internal int CurrentTime => Environment.TickCount - StartTime;
        internal long StartTime64 { get; } = Environment.TickCount64;
        internal long CurrentTime64 => Environment.TickCount64 - StartTime64;

        private SemaphoreSlim ConnectSempahore { get; }
        private HttpClient HttpClient { get; }
        public IWeatherTypeProvider WeatherTypeProvider { get; }
        public IWeatherProvider WeatherProvider { get; }
        private RainHelper RainHelper { get; }
        private TimeZoneInfo RealTimeZone { get; }
        private ITrackParamsProvider TrackParamsProvider { get; }
        public TrackParams.TrackParams TrackParams { get; }
        
        public TrafficMap TrafficMap { get; }
        public AiBehavior AiBehavior { get; }
        
        private List<PosixSignalRegistration> SignalHandlers { get; }

        public ACServer(ACServerConfiguration configuration)
        {
            Log.Information("Starting server.");

            ChatLog = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Sink((ILogEventSink)Log.Logger)
                .WriteTo.ChatLog(this)
                .CreateLogger();

            Configuration = configuration;
            EntryCars = Configuration.EntryCars.ToImmutableList();
            Log.Information("Loaded {0} cars.", EntryCars.Count);
            for (int i = 0; i < EntryCars.Count; i++)
            {
                EntryCars[i].SessionId = (byte)i;
                EntryCars[i].Server = this;
                EntryCars[i].OtherCarsLastSentUpdateTime = new long[EntryCars.Count];
                EntryCars[i].SetAiOverbooking(0);
            }
                                
            CurrentDaySeconds = (float)(Configuration.SunAngle * (50400.0 - 46800.0) / 16.0 + 46800.0);
            ConnectSempahore = new SemaphoreSlim(1, 1);
            ConnectedCars = new ConcurrentDictionary<int, EntryCar>();
            EndpointCars = new ConcurrentDictionary<IPEndPoint, EntryCar>();
            Blacklist = new ConcurrentDictionary<string, bool>();
            Admins = new ConcurrentDictionary<string, bool>();
            UdpServer = new ACUdpServer(this, Configuration.UdpPort);
            HttpClient = new HttpClient();
            CommandService = new CommandService(new CommandServiceConfiguration
            {
                DefaultRunMode = RunMode.Parallel
            });

            CommandService.AddModules(Assembly.GetEntryAssembly());
            CommandService.AddTypeParser(new ACClientTypeParser());
            CommandService.CommandExecutionFailed += OnCommandExecutionFailed;
            
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
                Log.Error("No track params found for {0}. Live weather and realtime will be disabled.", Configuration.Track);

                Configuration.Extra.EnableRealTime = false;
                Configuration.Extra.EnableLiveWeather = false;
            }
            else if (string.IsNullOrWhiteSpace(TrackParams.Timezone))
            {
                Configuration.Extra.EnableRealTime = false;
            }

            WeatherTypeProvider = new DefaultWeatherTypeProvider();
            RainHelper = new RainHelper();

            if (Configuration.Extra.EnableLiveWeather)
            {
                WeatherProvider = new LiveWeatherProvider(this);
            }
            else if (Configuration.Extra.EnableWeatherVoting)
            {
                WeatherProvider = new VotingWeatherProvider(this);
            }
            else
            {
                WeatherProvider = new DefaultWeatherProvider(this);
            }

            if (Configuration.Extra.EnableRealTime)
            {
                /*
                 * In theory TZConvert could be removed because .NET 6 supports IANA timezone names natively
                 * In practice the native way is not supported in Windows 10 LTSC 2019, so keeping this in for now
                 * https://docs.microsoft.com/en-us/windows/win32/intl/international-components-for-unicode--icu-
                 * ("icu.dll" is required which was added in Version 1903)
                 */
                RealTimeZone = TZConvert.GetTimeZoneInfo(TrackParams.Timezone);
                UpdateRealTime();

                Log.Information("Enabled real time with time zone {0}", RealTimeZone.DisplayName);
            }

            if (Configuration.Extra.EnableAi)
            {
                string mapAiBasePath = "content/tracks/" + Configuration.Track + "/ai/";
                if (File.Exists(mapAiBasePath + "traffic_map.obj"))
                {
                    TrafficMap = WavefrontObjParser.ParseFile(mapAiBasePath + "traffic_map.obj");
                } 
                else if (File.Exists(mapAiBasePath + "fast_lane.ai"))
                {
                    var parser = new FastLaneParser(this);
                    TrafficMap = parser.FromFile(mapAiBasePath + "fast_lane.ai");
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

            LoadBlacklist();
            LoadAdmins();

            Discord = new Discord(configuration.Extra);

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

        private void LoadAdmins()
        {
            const string adminsPath = "admins.txt";
            if (File.Exists(adminsPath))
            {
                foreach (string guid in File.ReadAllLines(adminsPath))
                    Admins[guid] = true;
            }
            else
                File.Create(adminsPath);
        }

        private void LoadBlacklist()
        {
            const string blacklistPath = "blacklist.txt";
            if (File.Exists(blacklistPath))
            {
                foreach (string guid in File.ReadAllLines(blacklistPath))
                    Blacklist[guid] = true;
            }
            else
                File.Create(blacklistPath);
        }

        public async Task StartAsync()
        {
            CurrentSession = Configuration.Sessions[0];
            CurrentSession.StartTime = DateTime.Now;
            CurrentSession.StartTimeTicks = CurrentTime;

            await InitializeGeoParams();
            await WeatherProvider.UpdateAsync();

            InitializeSteam();
            _ = Task.Factory.StartNew(AcceptTcpConnectionsAsync, TaskCreationOptions.LongRunning);
            UdpServer.Start();

            HttpServer = WebHost.CreateDefaultBuilder()
                .UseSerilog()
                .UseStartup(context => new Startup(this))
                .UseUrls($"http://*:{Configuration.HttpPort}")
                .Build();
            await HttpServer.StartAsync();
            
            if (Configuration.RegisterToLobby)
                await RegisterToLobbyAsync();
            
            _ = Task.Factory.StartNew(UpdateAsync, TaskCreationOptions.LongRunning);
        }

        private async Task InitializeGeoParams()
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
                Log.Information("Failed to get IP geolocation parameters.");
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
            LoadBlacklist();
            LoadAdmins();
            
            Log.Information("Reloaded blacklist and adminlist");
            context.Cancel = true;
        }

        private void UpdateRealTime()
        {
            var realTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, RealTimeZone);
            CurrentDaySeconds = (float)realTime.TimeOfDay.TotalSeconds;
            WeatherFxStartDate = new DateTimeOffset(DateTime.SpecifyKind(realTime.Date, DateTimeKind.Utc)).ToUnixTimeSeconds();
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
                await ConnectSempahore.WaitAsync();

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
                ConnectSempahore.Release();
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
                if (!reason.Equals(KickReason.ChecksumFailed))
                    Discord.SendAuditKickMessage(Configuration.Name, client, reasonStr, admin);
            }

            await client.DisconnectAsync();
        }

        public async ValueTask BanAsync(ACTcpClient client, KickReason reason, string reasonStr = null, ACTcpClient admin = null)
        {
            if (reasonStr != null)
                BroadcastPacket(new ChatMessage {SessionId = 255, Message = reasonStr});

            if (client != null)
            {
                Blacklist.TryAdd(client.Guid, true);

                Log.Information("{0} was banned. Reason: {1}", client.Name, reasonStr ?? "No reason given.");
                await File.WriteAllLinesAsync("blacklist.txt", Blacklist.Where(p => p.Value).Select(p => p.Key));
                client.SendPacket(new KickCar {SessionId = client.SessionId, Reason = reason});

                Discord.SendAuditBanMessage(Configuration.Name, client, reasonStr, admin);
                
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
            SendCurrentWeather();
        }
        
        public void SetCspWeather(WeatherFxType upcoming, int duration)
        {
            Log.Information("CSP weather transitioning to {0}", upcoming);
            
            CurrentWeather.UpcomingType = WeatherTypeProvider.GetWeatherType(upcoming);
            CurrentWeather.TransitionValue = 0;
            CurrentWeather.TransitionValueInternal = 0;
            CurrentWeather.TransitionDuration = duration * 1000;

            SendCurrentWeather();
        }

        public void SendRawFileTcp(string filename)
        {
            try
            {
                byte[] file = File.ReadAllBytes(filename);
                BroadcastPacket(new RawPacket()
                {
                    Content = file
                });
            }
            catch (Exception e)
            {
                Log.Error(e, "Could not send file");
            }
        }
        
        public void SendRawFileUdp(string filename)
        {
            try
            {
                byte[] file = File.ReadAllBytes(filename);
                BroadcastPacketUdp(new RawPacket()
                {
                    Content = file
                });
            }
            catch (Exception e)
            {
                Log.Error(e, "Could not send file");
            }
        }

        public void SetTime(float time)
        {
            CurrentDaySeconds = Math.Clamp(time, 0, 86400);
            Configuration.SunAngle = (float)(16.0 * (time - 46800.0) / (50400.0 - 46800.0));

            BroadcastPacket(new SunAngleUpdate { SunAngle = Configuration.SunAngle });
        }

        public void SendCurrentWeather(ACTcpClient endpoint = null)
        {
            if (Configuration.Extra.EnableWeatherFx)
            {
                var weather = new CSPWeatherUpdate
                {
                    UnixTimestamp = (ulong) DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    WeatherType = (byte) CurrentWeather.Type.WeatherFxType,
                    UpcomingWeatherType = (byte) CurrentWeather.UpcomingType.WeatherFxType,
                    TransitionValue = CurrentWeather.TransitionValue,
                    TemperatureAmbient = (Half) CurrentWeather.TemperatureAmbient,
                    TemperatureRoad = (Half) CurrentWeather.TemperatureRoad,
                    TrackGrip = (Half) CurrentWeather.TrackGrip,
                    WindDirectionDeg = (Half) CurrentWeather.WindDirection,
                    WindSpeed = (Half) CurrentWeather.WindSpeed,
                    Humidity = (Half) CurrentWeather.Humidity,
                    Pressure = (Half) CurrentWeather.Pressure,
                    RainIntensity = (Half) CurrentWeather.RainIntensity,
                    RainWetness = (Half) CurrentWeather.RainWetness,
                    RainWater = (Half) CurrentWeather.RainWater
                };

                Log.Information("CSP Weather: {0}", weather);

                if (endpoint == null)
                {
                    BroadcastPacketUdp(weather);
                }
                else
                {
                    endpoint.SendPacketUdp(weather);
                }
            }
            else
            {
                var weather = new WeatherUpdate
                {
                    Ambient = (byte) CurrentWeather.TemperatureAmbient,
                    Graphics = CurrentWeather.Type.Graphics,
                    Road = (byte) CurrentWeather.TemperatureRoad,
                    WindDirection = (short) CurrentWeather.WindDirection,
                    WindSpeed = (short) CurrentWeather.WindSpeed
                };
                
                Log.Information("Weather: {0}", weather);

                if (endpoint == null)
                {
                    BroadcastPacket(weather);
                }
                else
                {
                    endpoint.SendPacket(weather);
                }
            }
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
            long lastLobbyUpdate = 0;
            long lastTimeUpdate = Environment.TickCount64;
            long lastWeatherUpdate = Environment.TickCount64;
            long lastAiUpdate = Environment.TickCount64;
            long lastAiObstacleDetectionUpdate = Environment.TickCount64;
            float networkDistanceSquared = (float)Math.Pow(Configuration.Extra.NetworkBubbleDistance, 2);
            int outsideNetworkBubbleUpdateRateMs = 1000 / Configuration.Extra.OutsideNetworkBubbleRefreshRateHz;

            Log.Information("Starting update loop with an update rate of {0}hz.", Configuration.RefreshRateHz);
            while (true)
            {
                try
                {
                    if (Configuration.Extra.EnableAi)
                    {
                        foreach (EntryCar entryCar in EntryCars)
                        {
                            if (entryCar.AiControlled)
                            {
                                entryCar.AiUpdate();
                            }
                        }
                    }
                    
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
                                        
                                        //Log.Debug("sending PositionUpdate {0}", fromCar.Status.PakSequenceId);

                                        toClient.SendPacketUdp(new PositionUpdate
                                        {
                                            SessionId = fromCar.SessionId,
                                            EngineRpm = status.EngineRpm,
                                            Gas = status.Gas,
                                            Gear = status.Gear,
                                            LastRemoteTimestamp = fromCar.LastRemoteTimestamp,
                                            Timestamp = (uint) (fromCar.Status.Timestamp - toCar.TimeOffset),
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
                            
                                if(fromCar.Status.Position.Y < -500 && Environment.TickCount64 - fromCar.LastFallCheckTime > 1000)
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
                                        Timestamp = (uint) (status.Timestamp - toCar.TimeOffset),
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

                    if (Environment.TickCount64 - lastWeatherUpdate > Configuration.Extra.WeatherUpdateIntervalMilliseconds)
                    {
                        lastWeatherUpdate = Environment.TickCount64;
                        _ = WeatherProvider.UpdateAsync(CurrentWeather);
                    }

                    if (Environment.TickCount64 - lastLobbyUpdate > 60_000)
                    {
                        lastLobbyUpdate = Environment.TickCount64;
                        if (Configuration.RegisterToLobby)
                        {
                            _ = PingLobbyAsync();
                        }
                    }

                    if (Environment.TickCount64 - lastAiUpdate > 500)
                    {
                        lastAiUpdate = Environment.TickCount64;
                        if (Configuration.Extra.EnableAi)
                        {
                            _ = Task.Run(AiBehavior.Update)
                                .ContinueWith(t => Log.Error(t.Exception, "Error in AI update"), TaskContinuationOptions.OnlyOnFaulted);
                        }
                    }

                    if (Environment.TickCount64 - lastTimeUpdate > 1000)
                    {
                        if (Configuration.Extra.EnableRealTime)
                        {
                            UpdateRealTime();
                        }
                        else
                        {
                            CurrentDaySeconds += (Environment.TickCount64 - lastTimeUpdate) / 1000.0f * Configuration.TimeOfDayMultiplier;
                            if (CurrentDaySeconds <= 0)
                                CurrentDaySeconds = 0;
                            else if (CurrentDaySeconds >= 86400)
                                CurrentDaySeconds = 0;
                        }

                        if (Configuration.Extra.EnableWeatherFx)
                        {
                            RainHelper.Update(CurrentWeather, Configuration.DynamicTrack.BaseGrip, Configuration.Extra.RainTrackGripReduction, Environment.TickCount64 - lastTimeUpdate);
                            SendCurrentWeather();
                        }
                        else
                        {
                            SetTime(CurrentDaySeconds);
                        }

                        lastTimeUpdate = Environment.TickCount64;
                    }

                    if (Environment.TickCount64 - lastAiObstacleDetectionUpdate > 100)
                    {
                        lastAiObstacleDetectionUpdate = Environment.TickCount64;
                        if (Configuration.Extra.EnableAi)
                        {
                            _ = Task.Run(AiBehavior.ObstacleDetection)
                                .ContinueWith(t => Log.Error(t.Exception, "Error in AI obstacle detection"), TaskContinuationOptions.OnlyOnFaulted);
                        }
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

                    UdpServer.UpdateStatistics();

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
                await ConnectSempahore.WaitAsync();
                if (client.IsConnected && client.EntryCar?.Client == client && ConnectedCars.TryRemove(client.SessionId, out _))
                {
                    Log.Information("{0} has disconnected.", client.Name);

                    if (client.UdpEndpoint != null)
                        EndpointCars.TryRemove(client.UdpEndpoint, out _);

                    client.EntryCar.Client = null;
                    client.IsConnected = false;

                    if (client.HasPassedChecksum)
                        BroadcastPacket(new CarDisconnected { SessionId = client.SessionId });

                    if (Configuration.Extra.EnableAi && client.EntryCar.AiMode == AiMode.Auto)
                    {
                        client.EntryCar.SetAiControl(true);
                        AiBehavior.AdjustOverbooking();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error disconnecting {0}.", client?.Name);
            }
            finally
            {
                ConnectSempahore.Release();
            }
        }

        internal async ValueTask ProcessCommandAsync(ACTcpClient client, ChatMessage message)
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

        private async Task RegisterToLobbyAsync()
        {
            ACServerConfiguration cfg = Configuration;
            Dictionary<string, object> queryParamsDict = new Dictionary<string, object>
            {
                ["name"] = cfg.Name,
                ["port"] = cfg.UdpPort,
                ["tcp_port"] = cfg.TcpPort,
                ["max_clients"] = cfg.MaxClients,
                ["track"] = cfg.FullTrackName,
                ["cars"] = string.Join(',', cfg.EntryCars.Select(c => c.Model).Distinct()),
                ["timeofday"] = (int)cfg.SunAngle,
                ["sessions"] = string.Join(',', cfg.Sessions.Select(s => s.Type)),
                ["durations"] = string.Join(',', cfg.Sessions.Select(s => s.Type == 3 ? s.Laps : s.Time * 60)),
                ["password"] = string.IsNullOrEmpty(cfg.Password) ? "0" : cfg.Password,
                ["version"] = "202",
                ["pickup"] = "1",
                ["autoclutch"] = cfg.AutoClutchAllowed ? "1" : "0",
                ["abs"] = cfg.ABSAllowed,
                ["tc"] = cfg.TractionControlAllowed,
                ["stability"] = cfg.StabilityAllowed ? "1" : "0",
                ["legal_tyres"] = cfg.LegalTyres,
                ["fixed_setup"] = "0",
                ["timed"] = "0",
                ["extra"] = cfg.HasExtraLap ? "1" : "0",
                ["pit"] = "0",
                ["inverted"] = cfg.InvertedGridPositions
            };

            Log.Information("Registering server to lobby.");
            string queryString = string.Join('&', queryParamsDict.Select(p => $"{p.Key}={p.Value}"));
            HttpResponseMessage response = await HttpClient.GetAsync($"http://93.57.10.21/lobby.ashx/register?{queryString}");
            if (!response.IsSuccessStatusCode)
                Log.Information("Failed to register to lobby.");
        }

        private async Task PingLobbyAsync()
        {
            Dictionary<string, object> queryParamsDict = new Dictionary<string, object>
            {
                ["session"] = CurrentSession.Type,
                ["timeleft"] = (int)CurrentSession.TimeLeft.TotalSeconds,
                ["port"] = Configuration.UdpPort,
                ["clients"] = ConnectedCars.Count,
                ["track"] = Configuration.FullTrackName,
                ["pickup"] = "1"
            };

            string queryString = string.Join('&', queryParamsDict.Select(p => $"{p.Key}={p.Value}"));
            HttpResponseMessage response = await HttpClient.GetAsync($"http://93.57.10.21/lobby.ashx/ping?{queryString}");
            if (!response.IsSuccessStatusCode)
                Log.Information("Failed to send lobby ping update.");
        }

        private async Task AcceptTcpConnectionsAsync()
        {
            Log.Information("Starting TCP server on port {0}.", Configuration.TcpPort);
            TcpListener = new TcpListener(IPAddress.Any, Configuration.TcpPort);
            TcpListener.Start();

            while (true)
            {
                try
                {
                    TcpClient tcpClient = await TcpListener.AcceptTcpClientAsync();
                    Log.Information("Incoming TCP connection from {0}.", tcpClient.Client.RemoteEndPoint);

                    ACTcpClient acClient = new ACTcpClient(this, tcpClient);
                    await acClient.StartAsync();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Something went wrong while trying to accept TCP connection.");
                }
            }
        }

        private void InitializeSteam()
        {
            if (Configuration.Extra.UseSteamAuth)
            {
                var serverInit = new SteamServerInit("assettocorsa", "Assetto Corsa")
                {
                    GamePort = 9600,
                    Secure = true,
                    QueryPort = 28016
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
