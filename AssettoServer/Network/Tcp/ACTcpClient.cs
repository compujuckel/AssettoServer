using AssettoServer.Network.Packets;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AssettoServer.Network.Packets.Incoming;
using AssettoServer.Network.Packets.Outgoing;
using AssettoServer.Network.Packets.Outgoing.Handshake;
using AssettoServer.Network.Packets.Shared;
using AssettoServer.Network.Udp;
using AssettoServer.Server;
using AssettoServer.Server.Blacklist;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Weather;
using NanoSockets;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace AssettoServer.Network.Tcp
{
    public class ACTcpClient
    {
        private ACServer Server { get; }
        private ACUdpServer UdpServer { get; }
        public ILogger Logger { get; }
        public byte SessionId { get; set; }
        public string? Name { get; private set; }
        public string? Team { get; private set; }
        public string? NationCode { get; private set; }
        public bool IsAdministrator { get; internal set; }
        public string? Guid { get; internal set; }
        public EntryCar EntryCar { get; internal set; } = null!;
        public bool IsDisconnectRequested => _disconnectRequested == 1;
        public bool HasSentFirstUpdate { get; private set; }
        public bool IsConnected { get; set; }
        public TcpClient TcpClient { get; }

        private NetworkStream TcpStream { get; }
        [MemberNotNullWhen(true, nameof(Name), nameof(Team), nameof(NationCode), nameof(Guid))] 
        public bool HasStartedHandshake { get; private set; }
        public bool HasPassedChecksum { get; private set; }

        internal Address? UdpEndpoint { get; private set; }
        internal bool HasAssociatedUdp { get; private set; }
        internal bool SupportsCSPCustomUpdate { get; private set; }

        private ThreadLocal<byte[]> UdpSendBuffer { get; }
        private Memory<byte> TcpSendBuffer { get; }
        private Channel<IOutgoingNetworkPacket> OutgoingPacketChannel { get; }
        private CancellationTokenSource DisconnectTokenSource { get; }
        private Task SendLoopTask { get; set; } = null!;
        private long LastChatTime { get; set; }
        private int _disconnectRequested = 0;

        private readonly Steam _steam;
        private readonly WeatherManager _weatherManager;
        private readonly SessionManager _sessionManager;
        private readonly EntryCarManager _entryCarManager;
        private readonly ACServerConfiguration _configuration;
        private readonly IBlacklistService _blacklist;
        private readonly ChecksumManager _checksumManager;
        private readonly CSPFeatureManager _cspFeatureManager;
        private readonly CSPServerExtraOptions _cspServerExtraOptions;

        /// <summary>
        /// Fires when a client passed the checksum checks. This does not mean that the player has finished loading, use ClientFirstUpdateSent for that.
        /// </summary>
        public event EventHandler<ACTcpClient, EventArgs>? ChecksumPassed;
        
        /// <summary>
        /// Fires when a client failed the checksum check.
        /// </summary>
        public event EventHandler<ACTcpClient, EventArgs>? ChecksumFailed;
        
        /// <summary>
        /// Fires when a client has sent a chat message. Set ChatEventArgs.Cancel = true to stop it from being broadcast to other players.
        /// </summary>
        public event EventHandler<ACTcpClient, ChatMessageEventArgs>? ChatMessageReceived;
        
        /// <summary>
        /// Fires when a player has started disconnecting.
        /// </summary>
        public event EventHandler<ACTcpClient, EventArgs>? Disconnecting;
        
        /// <summary>
        /// Fires when a client has sent the first position update and is visible to other players.
        /// </summary>
        public event EventHandler<ACTcpClient, EventArgs>? FirstUpdateSent;

        /// <summary>
        /// Fires when a client collided with something. TargetCar will be null for environment collisions.
        /// There are up to 5 seconds delay before a collision is reported to the server.
        /// </summary>
        public event EventHandler<ACTcpClient, CollisionEventArgs>? Collision;

        public class ACTcpClientLogEventEnricher : ILogEventEnricher
        {
            private readonly ACTcpClient _client;

            public ACTcpClientLogEventEnricher(ACTcpClient client)
            {
                _client = client;
            }
            
            public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
            {
                var endpoint = (IPEndPoint)_client.TcpClient.Client.RemoteEndPoint!;
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ClientName", _client.Name));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ClientSteamId", _client.Guid));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ClientIpAddress", endpoint.Address.ToString()));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ClientPort", endpoint.Port));
            }
        }

        public ACTcpClient(ACServer server, ACUdpServer udpServer, Steam steam, TcpClient tcpClient, SessionManager sessionManager, WeatherManager weatherManager, ACServerConfiguration configuration, EntryCarManager entryCarManager, IBlacklistService blacklist, ChecksumManager checksumManager, CSPFeatureManager cspFeatureManager, CSPServerExtraOptions cspServerExtraOptions)
        {
            Server = server;
            UdpServer = udpServer;
            _steam = steam;
            Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.With(new ACTcpClientLogEventEnricher(this))
                .WriteTo.Logger(Log.Logger)
                .CreateLogger();

            UdpSendBuffer = new ThreadLocal<byte[]>(() => new byte[1500]);

            TcpClient = tcpClient;
            _sessionManager = sessionManager;
            _weatherManager = weatherManager;
            _configuration = configuration;
            _entryCarManager = entryCarManager;
            _blacklist = blacklist;
            _checksumManager = checksumManager;
            _cspFeatureManager = cspFeatureManager;
            _cspServerExtraOptions = cspServerExtraOptions;
            tcpClient.ReceiveTimeout = (int)TimeSpan.FromMinutes(5).TotalMilliseconds;
            tcpClient.SendTimeout = (int)TimeSpan.FromSeconds(30).TotalMilliseconds;
            tcpClient.LingerState = new LingerOption(true, 2);

            TcpStream = tcpClient.GetStream();

            TcpSendBuffer = new byte[8192 + (_cspServerExtraOptions.EncodedWelcomeMessage.Length * 4) + 2];
            OutgoingPacketChannel = Channel.CreateBounded<IOutgoingNetworkPacket>(256);
            DisconnectTokenSource = new CancellationTokenSource();
        }

        internal Task StartAsync()
        {
            SendLoopTask = Task.Run(SendLoopAsync);
            _ = Task.Run(ReceiveLoopAsync);

            return Task.CompletedTask;
        }

        public void SendPacket<TPacket>(TPacket packet) where TPacket : IOutgoingNetworkPacket
        {
            try
            {
                if (!OutgoingPacketChannel.Writer.TryWrite(packet) && !(packet is SunAngleUpdate) && !IsDisconnectRequested)
                {
                    Logger.Warning("Cannot write packet to TCP packet queue for {ClientName}, disconnecting", Name);
                    _ = DisconnectAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error sending {PacketName} to {ClientName}", typeof(TPacket).Name, Name);
            }
        }

        internal void SendPacketUdp<TPacket>(in TPacket packet) where TPacket : IOutgoingNetworkPacket
        {
            try
            {
                if (!UdpEndpoint.HasValue)
                {
                    throw new InvalidOperationException($"UDP endpoint not associated for {Name}");
                }
                
                byte[] buffer = UdpSendBuffer.Value!;
                PacketWriter writer = new PacketWriter(buffer);
                int bytesWritten = writer.WritePacket(in packet);

                UdpServer.Send(UdpEndpoint.Value, buffer, 0, bytesWritten);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error sending {PacketName} to {ClientName}", typeof(TPacket).Name, Name);
            }
        }

        private async Task SendLoopAsync()
        {
            try
            {
                await foreach (var packet in OutgoingPacketChannel.Reader.ReadAllAsync(DisconnectTokenSource.Token))
                {
                    if (packet is not SunAngleUpdate)
                    {
                        if (packet is AuthFailedResponse authResponse)
                            Logger.Debug("Sending {PacketName} ({AuthResponseReason})", packet.GetType().Name, authResponse.Reason);
                        else if (packet is ChatMessage chatMessage && chatMessage.SessionId == 255)
                            Logger.Verbose("Sending {PacketName} ({ChatMessage}) to {ClientName}", packet.GetType().Name, chatMessage.Message, Name);
                        else
                            Logger.Verbose("Sending {PacketName} to {ClientName}", packet.GetType().Name, Name);
                    }

                    PacketWriter writer = new PacketWriter(TcpStream, TcpSendBuffer);
                    writer.WritePacket(packet);

                    await writer.SendAsync(DisconnectTokenSource.Token);
                }
            }
            catch (ChannelClosedException) { }
            catch (ObjectDisposedException) { }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error sending TCP packet to {ClientName}", Name);
                _ = DisconnectAsync();
            }
        }

        private async Task ReceiveLoopAsync()
        {
            byte[] buffer = new byte[2046];
            NetworkStream stream = TcpStream;

            try
            {
                while (!DisconnectTokenSource.IsCancellationRequested)
                {
                    PacketReader reader = new PacketReader(stream, buffer);
                    reader.SliceBuffer(await reader.ReadPacketAsync());

                    if (reader.Buffer.Length == 0)
                        return;

                    byte id = reader.Read<byte>();
                    if (id != 0x82)
                        Logger.Verbose("Received TCP packet with ID {PacketId:X}", id);

                    if (!HasStartedHandshake && id != 0x3D)
                        return;

                    if (!HasStartedHandshake)
                    {
                        HandshakeRequest handshakeRequest = reader.ReadPacket<HandshakeRequest>();
                        if (handshakeRequest.Name.Length > 25)
                            handshakeRequest.Name = handshakeRequest.Name.Substring(0, 25);

                        Name = handshakeRequest.Name.Trim();

                        Logger.Information("{ClientName} ({ClientSteamId} - {ClientIpEndpoint}) is attempting to connect ({CarModel})", handshakeRequest.Name, handshakeRequest.Guid, TcpClient.Client.RemoteEndPoint?.ToString(), handshakeRequest.RequestedCar);

                        List<string> cspFeatures;
                        if (!string.IsNullOrEmpty(handshakeRequest.Features))
                        {
                            cspFeatures = handshakeRequest.Features.Split(',').ToList();
                            Logger.Debug("{ClientName} supports extra CSP features: {ClientFeatures}", handshakeRequest.Name, cspFeatures);
                        }
                        else
                        {
                            cspFeatures = new List<string>();
                        }

                        if (id != 0x3D || handshakeRequest.ClientVersion != 202)
                            SendPacket(new UnsupportedProtocolResponse());
                        else if (await _blacklist.IsBlacklistedAsync(ulong.Parse(handshakeRequest.Guid)))
                            SendPacket(new BlacklistedResponse());
                        else if (_configuration.Server.Password?.Length > 0 && handshakeRequest.Password != _configuration.Server.Password && handshakeRequest.Password != _configuration.Server.AdminPassword)
                            SendPacket(new WrongPasswordResponse());
                        else if (!_sessionManager.CurrentSession.Configuration.IsOpen)
                            SendPacket(new SessionClosedResponse());
                        else if (!_cspFeatureManager.ValidateHandshake(cspFeatures))
                            SendPacket(new AuthFailedResponse("Missing CSP features. Please update CSP and/or Content Manager."));
                        else if (_configuration.Extra.UseSteamAuth && !await _steam.ValidateSessionTicketAsync(handshakeRequest.SessionTicket, handshakeRequest.Guid, this))
                            SendPacket(new AuthFailedResponse("Steam authentication failed."));
                        else if (string.IsNullOrEmpty(handshakeRequest.Guid) || !(handshakeRequest.Guid.Length >= 6))
                            SendPacket(new AuthFailedResponse("Invalid Guid."));
                        else if (!_entryCarManager.ValidateHandshake(this, handshakeRequest, out var response))
                            SendPacket(response);
                        else if (!await _entryCarManager.TrySecureSlotAsync(this, handshakeRequest))
                            SendPacket(new NoSlotsAvailableResponse());
                        else
                        {
                            if (EntryCar == null)
                                throw new InvalidOperationException("No EntryCar set even though handshake started");

                            EntryCar.SetActive();
                            Team = handshakeRequest.Team;
                            NationCode = handshakeRequest.Nation;
                            Guid = handshakeRequest.Guid;
                            SupportsCSPCustomUpdate = _configuration.Extra.EnableCustomUpdate && cspFeatures.Contains("CUSTOM_UPDATE");

                            // Gracefully despawn AI cars
                            EntryCar.SetAiOverbooking(0);

                            if (!string.IsNullOrWhiteSpace(_configuration.Server.AdminPassword) && handshakeRequest.Password == _configuration.Server.AdminPassword)
                                IsAdministrator = true;

                            Logger.Information("{ClientName} ({ClientSteamId}, {SessionId} ({Car})) has connected", Name, Guid, SessionId, EntryCar.Model + "-" + EntryCar.Skin);

                            var cfg = _configuration.Server;
                            HandshakeResponse handshakeResponse = new HandshakeResponse
                            {
                                ABSAllowed = cfg.ABSAllowed,
                                TractionControlAllowed = cfg.TractionControlAllowed,
                                AllowedTyresOutCount = cfg.AllowedTyresOutCount,
                                AllowTyreBlankets = cfg.AllowTyreBlankets,
                                AutoClutchAllowed = cfg.AutoClutchAllowed,
                                CarModel = EntryCar.Model,
                                CarSkin = EntryCar.Skin,
                                FuelConsumptionRate = cfg.FuelConsumptionRate,
                                HasExtraLap = cfg.HasExtraLap,
                                InvertedGridPositions = cfg.InvertedGridPositions,
                                IsGasPenaltyDisabled = cfg.IsGasPenaltyDisabled,
                                IsVirtualMirrorForced = cfg.IsVirtualMirrorForced,
                                JumpStartPenaltyMode = cfg.JumpStartPenaltyMode,
                                MechanicalDamageRate = cfg.MechanicalDamageRate,
                                PitWindowEnd = cfg.PitWindowEnd,
                                PitWindowStart = cfg.PitWindowStart,
                                StabilityAllowed = cfg.StabilityAllowed,
                                RaceOverTime = cfg.RaceOverTime,
                                RefreshRateHz = cfg.RefreshRateHz,
                                ResultScreenTime = cfg.ResultScreenTime,
                                ServerName = cfg.Name,
                                SessionId = SessionId,
                                SunAngle = (float)WeatherUtils.SunAngleFromTicks(_weatherManager.CurrentDateTime.TimeOfDay.TickOfDay),
                                TrackConfig = cfg.TrackConfig,
                                TrackName = cfg.Track,
                                TyreConsumptionRate = cfg.TyreConsumptionRate,
                                UdpPort = cfg.UdpPort,
                                CurrentSession = _sessionManager.CurrentSession,
                                ChecksumCount = (byte)_checksumManager.TrackChecksums.Count,
                                ChecksumPaths = _checksumManager.TrackChecksums.Keys,
                                CurrentTime = (int)_sessionManager.ServerTimeMilliseconds,
                                LegalTyres = cfg.LegalTyres,
                                RandomSeed = 123,
                                SessionCount = (byte)_configuration.Sessions.Count,
                                Sessions = _configuration.Sessions,
                                SpawnPosition = SessionId,
                                TrackGrip = Math.Clamp(cfg.DynamicTrack != null ? cfg.DynamicTrack.BaseGrip + (cfg.DynamicTrack.GripPerLap * cfg.DynamicTrack.TotalLapCount) : 1, 0, 1),
                                MaxContactsPerKm = cfg.MaxContactsPerKm
                            };

                            HasStartedHandshake = true;
                            SendPacket(handshakeResponse);

                            _ = Task.Delay(TimeSpan.FromMinutes(10)).ContinueWith(async _ =>
                            {
                                if (EntryCar.Client == this && IsConnected && !HasSentFirstUpdate)
                                {
                                    Logger.Information("{ClientName} has taken over 10 minutes to spawn in and will be disconnected", Name);
                                    await DisconnectAsync();
                                }
                            });
                        }

                        if (!HasStartedHandshake)
                            return;
                    }
                    else if (HasStartedHandshake)
                    {
                        if (id == 0x3F)
                            OnCarListRequest(reader);
                        else if (id == 0xD)
                            OnP2PUpdate(reader);
                        else if (id == 0x50)
                            OnTyreCompoundChange(reader);
                        else if (id == 0x43)
                            return;
                        else if (id == 0x47)
                            OnChat(reader);
                        else if (id == 0x44)
                            await OnChecksumAsync(reader);
                        else if (id == 0x49)
                            OnLapCompletedMessageReceived(reader);
                        else if (id == 0xAB)
                        {
                            id = reader.Read<byte>();
                            Logger.Verbose("Received extended TCP packet with ID {PacketId:X}", id);

                            if (id == 0x00)
                                OnSpectateCar(reader);
                            else if (id == 0x03)
                                OnCSPClientMessage(reader);
                        }
                        else if (id == 0x82)
                            OnClientEvent(reader);
                    }
                }
            }
            catch (ObjectDisposedException) { }
            catch (IOException) { }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error receiving TCP packet from {ClientName}", Name);
            }
            finally
            {
                await DisconnectAsync();
            }
        }

        private void OnClientEvent(PacketReader reader)
        {
            ClientEvent clientEvent = reader.ReadPacket<ClientEvent>();

            foreach (var evt in clientEvent.ClientEvents)
            {
                EntryCar? targetCar = null;
                
                if (evt.Type == ClientEvent.ClientEventType.PlayerCollision)
                {
                    targetCar = _entryCarManager.EntryCars[evt.TargetSessionId];
                    Logger.Information("Collision between {SourceCarName} ({SourceCarSessionId}) and {TargetCarName} ({TargetCarSessionId}), rel. speed {Speed:F0}km/h", 
                        Name, EntryCar.SessionId, targetCar.Client?.Name ?? targetCar.AiName, targetCar.SessionId, evt.Speed);
                }
                else
                {
                    Logger.Information("Collision between {SourceCarName} ({SourceCarSessionId}) and environment, rel. speed {Speed:F0}km/h", 
                        Name, EntryCar.SessionId, evt.Speed);
                }
                
                Collision?.Invoke(this, new CollisionEventArgs(targetCar, evt.Speed, evt.Position, evt.RelPosition));
            }
        }

        private void OnSpectateCar(PacketReader reader)
        {
            SpectateCar spectatePacket = reader.ReadPacket<SpectateCar>();
            EntryCar.TargetCar = spectatePacket.SessionId != SessionId ? _entryCarManager.EntryCars[spectatePacket.SessionId] : null;
        }

        private void OnCSPClientMessage(PacketReader reader)
        {
            CSPClientMessageType packetType = (CSPClientMessageType)reader.Read<ushort>();
            if (packetType == CSPClientMessageType.LuaMessage)
            {
                uint luaPacketType = reader.Read<uint>();

                if (Server.CSPClientMessageTypes.TryGetValue(luaPacketType, out var handler))
                {
                    handler(this, reader);
                }
                else
                {
                    CSPClientMessage clientMessage = reader.ReadPacket<CSPClientMessage>();
                    clientMessage.Type = packetType;
                    clientMessage.LuaType = luaPacketType;
                    clientMessage.SessionId = SessionId;

                    Logger.Debug("Unknown CSP lua client message with type 0x{LuaType:X} received, data {Data}", clientMessage.LuaType, Convert.ToHexString(clientMessage.Data));
                    _entryCarManager.BroadcastPacket(clientMessage);
                }
            }
            else
            {
                CSPClientMessage clientMessage = reader.ReadPacket<CSPClientMessage>();
                clientMessage.Type = packetType;
                clientMessage.SessionId = SessionId;
                
                _entryCarManager.BroadcastPacket(clientMessage);
            }
        }

        private async ValueTask OnChecksumAsync(PacketReader reader)
        {
            bool passedChecksum = false;
            byte[] fullChecksum = new byte[16 * (_checksumManager.TrackChecksums.Count + 1)];
            if (reader.Buffer.Length == fullChecksum.Length + 1)
            {
                reader.ReadBytes(fullChecksum);
                passedChecksum = !_checksumManager.CarChecksums.TryGetValue(EntryCar.Model, out byte[]? modelChecksum) || fullChecksum.AsSpan().Slice(fullChecksum.Length - 16).SequenceEqual(modelChecksum);

                KeyValuePair<string, byte[]>[] allChecksums = _checksumManager.TrackChecksums.ToArray();
                for (int i = 0; i < allChecksums.Length; i++)
                    if (!allChecksums[i].Value.AsSpan().SequenceEqual(fullChecksum.AsSpan().Slice(i * 16, 16)))
                    {
                        Logger.Information("{ClientName} failed checksum for file {ChecksumFile}", Name, allChecksums[i].Key);
                        passedChecksum = false;
                        break;
                    }
            }

            HasPassedChecksum = passedChecksum;
            if (!passedChecksum)
            {
                ChecksumFailed?.Invoke(this, EventArgs.Empty);
                await _entryCarManager.KickAsync(this, KickReason.ChecksumFailed, $"{Name} failed the checksum check and has been kicked.", false);
            }
            else
            {
                ChecksumPassed?.Invoke(this, EventArgs.Empty);

                _entryCarManager.BroadcastPacket(new CarConnected
                {
                    SessionId = SessionId,
                    Name = Name,
                    Nation = NationCode
                }, this);
            }
        }

        private void OnChat(PacketReader reader)
        {
            long currentTime = _sessionManager.ServerTimeMilliseconds;
            if (currentTime - LastChatTime < 1000)
                return;
            LastChatTime = currentTime;

            if (_configuration.Extra.AfkKickBehavior == AfkKickBehavior.PlayerInput)
            {
                EntryCar.SetActive();
            }

            ChatMessage chatMessage = reader.ReadPacket<ChatMessage>();
            chatMessage.SessionId = SessionId;
            
            Logger.Information("CHAT: {ClientName} ({SessionId}): {ChatMessage}", Name, SessionId, chatMessage.Message);

            var args = new ChatMessageEventArgs
            {
                ChatMessage = chatMessage
            };
            ChatMessageReceived?.Invoke(this, args);
        }

        private void OnTyreCompoundChange(PacketReader reader)
        {
            TyreCompoundChangeRequest compoundChangeRequest = reader.ReadPacket<TyreCompoundChangeRequest>();
            EntryCar.Status.CurrentTyreCompound = compoundChangeRequest.CompoundName;

            _entryCarManager.BroadcastPacket(new TyreCompoundUpdate
            {
                CompoundName = compoundChangeRequest.CompoundName,
                SessionId = SessionId
            });
        }

        private void OnP2PUpdate(PacketReader reader)
        {
            // ReSharper disable once InconsistentNaming
            P2PUpdateRequest p2pUpdateRequest = reader.ReadPacket<P2PUpdateRequest>();
            if (p2pUpdateRequest.P2PCount == -1)
            {
                SendPacket(new P2PUpdate
                {
                    Active = false,
                    P2PCount = EntryCar.Status.P2PCount,
                    SessionId = SessionId
                });
            }
            else
            {
                _entryCarManager.BroadcastPacket(new P2PUpdate
                {
                    Active = EntryCar.Status.P2PActive,
                    P2PCount = EntryCar.Status.P2PCount,
                    SessionId = SessionId
                });
            }
        }

        private void OnCarListRequest(PacketReader reader)
        {
            CarListRequest carListRequest = reader.ReadPacket<CarListRequest>();

            List<EntryCar> carsInPage = _entryCarManager.EntryCars.Skip(carListRequest.PageIndex).Take(10).ToList();
            CarListResponse carListResponse = new CarListResponse()
            {
                PageIndex = carListRequest.PageIndex,
                EntryCarsCount = carsInPage.Count,
                EntryCars = carsInPage
            };

            SendPacket(carListResponse);
        }

        private void OnLapCompletedMessageReceived(PacketReader reader)
        {
            LapCompletedIncoming lapPacket = reader.ReadPacket<LapCompletedIncoming>();

            //_configuration.DynamicTrack.TotalLapCount++; // TODO reset at some point
            if (OnLapCompleted(lapPacket))
            {
                Server.SendLapCompletedMessage(SessionId, lapPacket.LapTime, lapPacket.Cuts);
            }
        }

        private bool OnLapCompleted(LapCompletedIncoming lap)
        {
            int timestamp = (int)_sessionManager.ServerTimeMilliseconds;

            var entryCarResult = _sessionManager.CurrentSession.Results?[SessionId] ?? throw new InvalidOperationException("Current session does not have results set");

            if (entryCarResult.HasCompletedLastLap)
            {
                Logger.Debug("Lap rejected by {ClientName}, already finished", Name);
                return false;
            }

            if (_sessionManager.CurrentSession.Configuration.Type == SessionType.Race && entryCarResult.NumLaps >= _sessionManager.CurrentSession.Configuration.Laps && !_sessionManager.CurrentSession.Configuration.IsTimedRace)
            {
                Logger.Debug("Lap rejected by {ClientName}, race over", Name);
                return false;
            }

            Logger.Information("Lap completed by {ClientName}, {NumCuts} cuts, laptime {LapTime}", Name, lap.Cuts, lap.LapTime);

            // TODO unfuck all of this

            if (_sessionManager.CurrentSession.Configuration.Type == SessionType.Race || lap.Cuts == 0)
            {
                entryCarResult.LastLap = lap.LapTime;
                if (lap.LapTime < entryCarResult.BestLap)
                {
                    entryCarResult.BestLap = lap.LapTime;
                }

                entryCarResult.NumLaps++;
                if (entryCarResult.NumLaps > _sessionManager.CurrentSession.LeaderLapCount)
                {
                    _sessionManager.CurrentSession.LeaderLapCount = entryCarResult.NumLaps;
                }

                entryCarResult.TotalTime = _sessionManager.CurrentSession.SessionTimeMilliseconds - (EntryCar.Ping / 2);

                if (_sessionManager.CurrentSession.SessionOverFlag)
                {
                    if (_sessionManager.CurrentSession.Configuration.Type == SessionType.Race && _sessionManager.CurrentSession.Configuration.IsTimedRace)
                    {
                        if (_configuration.Server.HasExtraLap)
                        {
                            if (entryCarResult.NumLaps <= _sessionManager.CurrentSession.LeaderLapCount)
                            {
                                entryCarResult.HasCompletedLastLap = _sessionManager.CurrentSession.LeaderHasCompletedLastLap;
                            }
                            else if (_sessionManager.CurrentSession.TargetLap > 0)
                            {
                                if (entryCarResult.NumLaps >= _sessionManager.CurrentSession.TargetLap)
                                {
                                    _sessionManager.CurrentSession.LeaderHasCompletedLastLap = true;
                                    entryCarResult.HasCompletedLastLap = true;
                                }
                            }
                            else
                            {
                                _sessionManager.CurrentSession.TargetLap = entryCarResult.NumLaps + 1;
                            }
                        }
                        else if (entryCarResult.NumLaps <= _sessionManager.CurrentSession.LeaderLapCount)
                        {
                            entryCarResult.HasCompletedLastLap = _sessionManager.CurrentSession.LeaderHasCompletedLastLap;
                        }
                        else
                        {
                            _sessionManager.CurrentSession.LeaderHasCompletedLastLap = true;
                            entryCarResult.HasCompletedLastLap = true;
                        }
                    }
                    else
                    {
                        entryCarResult.HasCompletedLastLap = true;
                    }
                }

                if (_sessionManager.CurrentSession.Configuration.Type != SessionType.Race)
                {
                    if (_sessionManager.CurrentSession.EndTime != 0)
                    {
                        entryCarResult.HasCompletedLastLap = true;
                    }
                }
                else if (_sessionManager.CurrentSession.Configuration.IsTimedRace)
                {
                    if (_sessionManager.CurrentSession.LeaderHasCompletedLastLap && _sessionManager.CurrentSession.EndTime == 0)
                    {
                        _sessionManager.CurrentSession.EndTime = timestamp;
                    }
                }
                else if (entryCarResult.NumLaps != _sessionManager.CurrentSession.Configuration.Laps)
                {
                    if (_sessionManager.CurrentSession.EndTime != 0)
                    {
                        entryCarResult.HasCompletedLastLap = true;
                    }
                }
                else if (!entryCarResult.HasCompletedLastLap)
                {
                    entryCarResult.HasCompletedLastLap = true;
                    if (_sessionManager.CurrentSession.EndTime == 0)
                    {
                        _sessionManager.CurrentSession.EndTime = timestamp;
                    }
                }
                else if (_sessionManager.CurrentSession.EndTime != 0)
                {
                    entryCarResult.HasCompletedLastLap = true;
                }

                return true;
            }

            if (_sessionManager.CurrentSession.EndTime == 0)
                return true;

            entryCarResult.HasCompletedLastLap = true;
            return false;
        }

        internal void SendFirstUpdate()
        {
            if (HasSentFirstUpdate)
                return;

            TcpClient.ReceiveTimeout = 0;
            EntryCar.LastPongTime = (int)_sessionManager.ServerTimeMilliseconds;
            HasSentFirstUpdate = true;

            List<EntryCar> connectedCars = _entryCarManager.EntryCars.Where(c => c.Client != null || c.AiControlled).ToList();

            if (!string.IsNullOrEmpty(_cspServerExtraOptions.EncodedWelcomeMessage))
                SendPacket(new WelcomeMessage { Message = _cspServerExtraOptions.EncodedWelcomeMessage });

            SendPacket(new DriverInfoUpdate { ConnectedCars = connectedCars });
            _weatherManager.SendWeather(this);

            foreach (EntryCar car in connectedCars)
            {
                SendPacket(new MandatoryPitUpdate { MandatoryPit = car.Status.MandatoryPit, SessionId = car.SessionId });
                if (car != EntryCar)
                    SendPacket(new TyreCompoundUpdate { SessionId = car.SessionId, CompoundName = car.Status.CurrentTyreCompound });

                if (_configuration.Extra.AiParams.HideAiCars)
                {
                    SendPacket(new CSPCarVisibilityUpdate
                    {
                        SessionId = car.SessionId,
                        Visible = car.AiControlled ? CSPCarVisibility.Invisible : CSPCarVisibility.Visible
                    });
                }
            }

            Server.SendLapCompletedMessage(255, 0, 0, this);

            _ = Task.Delay(40000).ContinueWith(async _ =>
            {
                if (!HasPassedChecksum && IsConnected)
                {
                    await _entryCarManager.KickAsync(this, KickReason.ChecksumFailed, $"{Name} did not send the requested checksums.", false);
                }
            });
            
            FirstUpdateSent?.Invoke(this, EventArgs.Empty);
        }

        internal bool TryAssociateUdp(Address endpoint)
        {
            if (HasAssociatedUdp)
                return false;

            UdpEndpoint = endpoint;
            HasAssociatedUdp = true;

            return true;
        }

        internal async Task DisconnectAsync()
        {
            try
            {
                if (Interlocked.CompareExchange(ref _disconnectRequested, 1, 0) == 1)
                    return;

                if (!string.IsNullOrEmpty(Name))
                {
                    Logger.Debug("Disconnecting {ClientName} ({$ClientIpEndpoint})", Name, TcpClient.Client.RemoteEndPoint);
                    Disconnecting?.Invoke(this, EventArgs.Empty);
                }

                OutgoingPacketChannel.Writer.TryComplete();
                _ = await Task.WhenAny(Task.Delay(2000), SendLoopTask);

                try
                {
                    DisconnectTokenSource.Cancel();
                    DisconnectTokenSource.Dispose();
                }
                catch (ObjectDisposedException) { }
                
                if (IsConnected)
                    await _entryCarManager.DisconnectClientAsync(this);

                TcpClient.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error disconnecting {ClientName}", Name);
            }
        }
    }
}
