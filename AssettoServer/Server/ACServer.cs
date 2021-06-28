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
using AssettoServer.Commands;
using AssettoServer.Network.Udp;
using AssettoServer.Network.Tcp;
using AssettoServer.Network.Http;
using AssettoServer.Network.Packets;
using AssettoServer.Commands.TypeParsers;
using AssettoServer.Server.Configuration;
using AssettoServer.Network.Packets.Shared;
using AssettoServer.Network.Packets.Incoming;
using AssettoServer.Network.Packets.Outgoing;
using Serilog;
using Serilog.Core;
using Qmmands;
using Steamworks;

namespace AssettoServer.Server
{
    public class ACServer
    {
        public ACServerConfiguration Configuration { get; }
        public SessionConfiguration CurrentSession { get; private set; }
        public WeatherConfiguration CurrentWeather { get; private set; }
        public float CurrentDayTime { get; private set; }

        internal ConcurrentDictionary<int, EntryCar> ConnectedCars { get; }
        internal ConcurrentDictionary<IPEndPoint, EntryCar> EndpointCars { get; }
        internal IReadOnlyList<EntryCar> EntryCars { get; }
        internal ConcurrentDictionary<string, bool> Blacklist { get; }
        internal ConcurrentDictionary<string, bool> Admins { get; }
        internal IReadOnlyDictionary<string, byte[]> TrackChecksums { get; private set; }
        internal IReadOnlyDictionary<string, byte[]> CarChecksums { get; private set; }
        internal CommandService CommandService { get; }
        internal Logger Log { get; }
        internal TcpListener TcpListener { get; set; }
        internal ACUdpServer UdpServer { get; }
        internal ACHttpServer HttpServer { get; }

        internal int StartTime { get; } = Environment.TickCount;
        internal int CurrentTime => Environment.TickCount - StartTime;
        internal long StartTime64 { get; } = Environment.TickCount64;
        internal long CurrentTime64 => Environment.TickCount64 - StartTime64;

        private SemaphoreSlim ConnectSempahore { get; }
        private HttpClient HttpClient { get; }

        public ACServer(ACServerConfiguration configuration)
        {

            Log = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File($"logs/{DateTime.Now:MMddyyyy_HHmmss}.txt")
                .CreateLogger();

            Log.Information("Starting server.");

            Configuration = configuration;
            EntryCars = Configuration.EntryCars.ToList();
            Log.Information("Loaded {0} cars.", EntryCars.Count);
            for (int i = 0; i < EntryCars.Count; i++)
            {
                EntryCars[i].SessionId = (byte)i;
                EntryCars[i].Server = this;
                EntryCars[i].OtherCarsLastSentUpdateTime = new long[EntryCars.Count];
            }
                                
            CurrentDayTime = (Configuration.SunAngle + 180) / 360 * 24;
            ConnectSempahore = new SemaphoreSlim(1, 1);
            ConnectedCars = new ConcurrentDictionary<int, EntryCar>();
            EndpointCars = new ConcurrentDictionary<IPEndPoint, EntryCar>();
            Blacklist = new ConcurrentDictionary<string, bool>();
            Admins = new ConcurrentDictionary<string, bool>();
            HttpServer = new ACHttpServer(this, IPAddress.Any, Configuration.HttpPort);
            UdpServer = new ACUdpServer(this, Configuration.UdpPort);
            HttpClient = new HttpClient();
            CommandService = new CommandService(new CommandServiceConfiguration
            {
                DefaultRunMode = RunMode.Parallel
            });

            CommandService.AddModules(Assembly.GetEntryAssembly());
            CommandService.AddTypeParser(new ACClientTypeParser());
            CommandService.CommandExecutionFailed += OnCommandExecutionFailed;

            string blacklistPath = "blacklist.txt";
            if (File.Exists(blacklistPath))
            {
                foreach (string guid in File.ReadAllLines(blacklistPath))
                    Blacklist[guid] = true;
            }
            else
                File.Create(blacklistPath);

            string adminsPath = "admins.txt";
            if (File.Exists(adminsPath))
            {
                foreach (string guid in File.ReadAllLines(adminsPath))
                    Admins[guid] = true;
            }
            else
                File.Create(adminsPath);

            InitializeChecksums();
        }

        public async Task StartAsync()
        {
            CurrentSession = Configuration.Sessions[0];
            CurrentSession.StartTime = DateTime.Now;
            CurrentSession.StartTimeTicks = CurrentTime;

            CurrentWeather = Configuration.Weathers[0];

            InitializeSteam();
            _ = Task.Factory.StartNew(AcceptTcpConnectionsAsync, TaskCreationOptions.LongRunning);
            UdpServer.Start();

#if !DEBUG
            await RegisterToLobbyAsync();
#else
            await Task.Yield();
#endif

            _ = Task.Factory.StartNew(UpdateAsync, TaskCreationOptions.LongRunning);
            HttpServer.Start();
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

                    if (entryCar.Client == null && handshakeRequest.RequestedCar == entryCar.Model)
                    {
                        entryCar.Reset();
                        entryCar.Client = client;
                        client.EntryCar = entryCar;
                        client.SessionId = entryCar.SessionId;
                        client.IsConnected = true;

                        if (!string.IsNullOrEmpty(handshakeRequest.Guid) && Admins.ContainsKey(handshakeRequest.Guid))
                            client.IsAdministrator = true;

                        ConnectedCars[client.SessionId] = entryCar;

                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error securing slot for {0}: {1}", client.Name, ex);
                Log.Error(ex, "Error securing slot for {0}.", client.Name);
            }
            finally
            {
                ConnectSempahore.Release();
            }

            return false;
        }

        public async Task KickAsync(ACTcpClient client, KickReason reason, string reasonStr = null, bool broadcastMessage = true)
        {
            if (reasonStr != null && broadcastMessage)
                BroadcastPacket(new ChatMessage { SessionId = 255, Message = reasonStr });

            if (client != null)
            {
                Log.Information("{0} was kicked. Reason: {1}", client.Name, reasonStr);
                client.SendPacket(new KickCar { SessionId = client.SessionId, Reason = reason });
            }

            await client.DisconnectAsync();
        }

        public async ValueTask BanAsync(ACTcpClient client, KickReason reason, string reasonStr = null)
        {
            if (reasonStr != null)
                BroadcastPacket(new ChatMessage { SessionId = 255, Message = reasonStr });

            if (client != null)
            {
                Blacklist.TryAdd(client.Guid, true);

                Log.Information("{0} was banned. Reason: {1}", client.Name, reasonStr);
                await File.WriteAllLinesAsync("blacklist.txt", Blacklist.Where(p => !p.Value).Select(p => p.Key));
                client.SendPacket(new KickCar { SessionId = client.SessionId, Reason = reason });
            }

            await client.DisconnectAsync();
        }

        public async ValueTask UnbanAsync(string guid)
        {
            if (Blacklist.TryRemove(guid, out _))
                await File.WriteAllLinesAsync("blacklist.txt", Blacklist.Where(p => !p.Value).Select(p => p.Key));
        }

        public void SetWeather(WeatherConfiguration weather)
        {
            Log.Information("Weather has been set to {0}.", weather.Graphics);
            CurrentWeather = weather;

            BroadcastPacket(new WeatherUpdate
            {
                Ambient = (byte)weather.BaseTemperatureAmbient,
                Graphics = weather.Graphics,
                Road = (byte)weather.BaseTemperatureRoad,
                WindDirection = (short)weather.WindBaseDirection,
                WindSpeed = (short)weather.WindBaseSpeedMin
            });
        }

        public void SetTime(float time)
        {
            CurrentDayTime = Math.Clamp(time, 0, 24);
            Configuration.SunAngle = (float)(((time - 8) / 0.0625) - 80);

            BroadcastPacket(new SunAngleUpdate { SunAngle = Configuration.SunAngle });
        }

        public void BroadcastPacket<TPacket>(TPacket packet, ACTcpClient sender = null) where TPacket : IOutgoingNetworkPacket
        {
            if (!(packet is SunAngleUpdate))
                Log.Debug("Broadcasting {0}", typeof(TPacket).Name);

            foreach (EntryCar car in EntryCars.Where(c => c.Client != null && c.Client.HasSentFirstUpdate && sender != c.Client))
                car.Client.SendPacket(packet);
        }

        public async Task UpdateAsync()
        {
            int sleepMs = 1000 / Configuration.RefreshRateHz;
            long nextTick = Environment.TickCount64;
            byte[] buffer = new byte[2048];
            long lastLobbyUpdate = 0;
            long lastTimeUpdate = 0;
            float networkDistanceSquared = (float)Math.Pow(Configuration.Extra.NetworkBubbleDistance, 2);
            int outsideNetworkBubbleUpdateRateMs = 1000 / Configuration.Extra.OutsideNetworkBubbleRefreshRateHz;

            Log.Information("Starting update loop with an update rate of {0}hz.", Configuration.RefreshRateHz);
            while (true)
            {
                try
                {
                    foreach (EntryCar fromCar in EntryCars)
                    {
                        ACTcpClient fromClient = fromCar.Client;
                        if (fromClient != null)
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

                                        PacketWriter writer = new PacketWriter(buffer);
                                        int bytesWritten = writer.WritePacket(new PositionUpdate
                                        {
                                            SessionId = fromClient.SessionId,
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
                                            StatusFlag = (Configuration.Extra.ForceLights || fromCar.ForceLights) ? status.StatusFlag | 0x20 : status.StatusFlag,
                                            SteerAngle = status.SteerAngle,
                                            TyreAngularSpeedFL = status.TyreAngularSpeed[0],
                                            TyreAngularSpeedFR = status.TyreAngularSpeed[1],
                                            TyreAngularSpeedRL = status.TyreAngularSpeed[2],
                                            TyreAngularSpeedRR = status.TyreAngularSpeed[3],
                                            Velocity = status.Velocity,
                                            WheelAngle = status.WheelAngle
                                        });

                                        //await UdpClient.SendAsync(buffer, bytesWritten, toClient.UdpEndpoint);
                                        //await UdpClient.Client.SendToAsync(new ArraySegment<byte>(buffer, 0, bytesWritten), SocketFlags.None, toClient.UdpEndpoint);
                                        UdpServer.Send(toClient.UdpEndpoint, buffer, 0, bytesWritten);
                                    }
                                }
                            
                                if(fromCar.Status.Position.Y < -500 && Environment.TickCount64 - fromCar.LastFallCheckTime > 1000)
                                {
                                    fromCar.LastFallCheckTime = Environment.TickCount64;
                                    fromCar.Client?.SendCurrentSession();
                                }
                            }

                            if (fromClient.HasSentFirstUpdate && (CurrentTime - fromCar.LastPingTime) > 1000)
                            {
                                fromCar.CheckAfk();
                                fromCar.LastPingTime = CurrentTime;

                                PacketWriter writer = new PacketWriter(buffer);
                                int bytesWritten = writer.WritePacket(new PingUpdate { CurrentPing = fromCar.Ping, Time = CurrentTime });

                                UdpServer.SendAsync(fromClient.UdpEndpoint, buffer, 0, bytesWritten);

                                if (CurrentTime - fromCar.LastPongTime > 15000)
                                {
                                    Log.Information("{0} has not sent a ping response for over 15 seconds.", fromCar?.Client?.Name);
                                    _ = fromCar.Client?.DisconnectAsync();
                                }
                            }
                        }
                    }

                    if (Environment.TickCount64 - lastLobbyUpdate > 60000)
                    {
                        lastLobbyUpdate = Environment.TickCount64;
#if !DEBUG
                        _ = PingLobbyAsync();
#endif
                    }

                    if (Environment.TickCount64 - lastTimeUpdate > 1000)
                    {
                        CurrentDayTime += (Environment.TickCount64 - lastTimeUpdate) / 1000 * Configuration.TimeOfDayMultiplier / 3600;
                        if (CurrentDayTime <= 0)
                            CurrentDayTime = 0;
                        else if (CurrentDayTime >= 24)
                            CurrentDayTime = 0;

                        SetTime(CurrentDayTime);
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

                    UdpServer.UpdateStatistics();

                    if (ConnectedCars.Count > 1)
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

            //Console.WriteLine("Sending lobby update ping.");
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
                    SteamServer.LogOnAnonymous();
                }
                catch (Exception ex)
                {
                    Log.Fatal(ex, "Error trying to initialize SteamServer.");
                }
            }
        }
    }
}
