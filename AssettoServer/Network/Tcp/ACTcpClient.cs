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
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Weather;
using Serilog;

namespace AssettoServer.Network.Tcp
{
    public class ACTcpClient
    {
        public ACServer Server { get; }
        public byte SessionId { get; set; }
        public string? Name { get; private set; }
        public string? Team { get; private set; }
        public string? NationCode { get; private set; }
        public bool IsAdministrator { get; internal set; }
        public string? Guid { get; internal set; }
        [NotNull] public EntryCar? EntryCar { get; set; }
        public bool IsDisconnectRequested => _disconnectRequested == 1;

        internal TcpClient TcpClient { get; }
        internal NetworkStream TcpStream { get; }
        internal bool HasSentFirstUpdate { get; private set; }
        [MemberNotNullWhen(true, nameof(Name), nameof(Team), nameof(NationCode), nameof(Guid))] internal bool HasStartedHandshake { get; private set; }
        internal bool HasPassedChecksum { get; private set; }
        internal bool IsConnected { get; set; }

        internal IPEndPoint? UdpEndpoint { get; private set; }
        internal bool HasAssociatedUdp { get; private set; }

        private ThreadLocal<byte[]> UdpSendBuffer { get; }
        private Memory<byte> TcpSendBuffer { get; }
        private SemaphoreSlim TcpSendSemaphore { get; }
        private Channel<IOutgoingNetworkPacket> OutgoingPacketChannel { get; }
        private CancellationTokenSource DisconnectTokenSource { get; }
        [NotNull] private Task? SendLoopTask { get; set; }
        private long LastChatTime { get; set; }
        private int _disconnectRequested = 0;

        public event EventHandler<ACTcpClient, ClientHandshakeEventArgs>? HandshakeStarted;
        public event EventHandler<ACTcpClient, EventArgs>? ChecksumPassed;
        public event EventHandler<ACTcpClient, EventArgs>? ChecksumFailed;
        public event EventHandler<ACTcpClient, ChatMessageEventArgs>? ChatMessageReceived;
        public event EventHandler<ACTcpClient, EventArgs>? Disconnecting;

        internal ACTcpClient(ACServer server, TcpClient tcpClient)
        {
            Server = server;

            UdpSendBuffer = new ThreadLocal<byte[]>(() => new byte[2048]);

            TcpClient = tcpClient;
            tcpClient.ReceiveTimeout = (int)TimeSpan.FromMinutes(5).TotalMilliseconds;
            tcpClient.SendTimeout = (int)TimeSpan.FromSeconds(30).TotalMilliseconds;
            tcpClient.LingerState = new LingerOption(true, 2);

            TcpStream = tcpClient.GetStream();

            TcpSendBuffer = new byte[8192 + ((server.CSPServerExtraOptions.EncodedWelcomeMessage?.Length ?? 0) * 4) + 2];
            TcpSendSemaphore = new SemaphoreSlim(1, 1);
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
                    Log.Warning("Cannot write packet to TCP packet queue for {0}, disconnecting", Name);
                    _ = DisconnectAsync();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error sending {0} to {1}.", typeof(TPacket).Name, Name);
            }
        }

        internal void SendPacketUdp<TPacket>(TPacket packet) where TPacket : IOutgoingNetworkPacket
        {
            try
            {
                byte[] buffer = UdpSendBuffer.Value!;
                PacketWriter writer = new PacketWriter(buffer);
                int bytesWritten = writer.WritePacket(packet);

                Server.UdpServer.Send(UdpEndpoint, buffer, 0, bytesWritten);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error sending {0} to {1}.", typeof(TPacket).Name, Name);
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
                            Log.Debug("Sending {0} ({1})", packet.GetType().Name, authResponse.Reason);
                        else if (packet is ChatMessage chatMessage && chatMessage.SessionId == 255)
                            Log.Verbose("Sending {0} ({1}) to {2}", packet.GetType().Name, chatMessage.Message, Name);
                        else
                            Log.Verbose("Sending {0} to {1}", packet.GetType().Name, Name);
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
                Log.Error(ex, "Error sending TCP packet to {0}", Name);
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
                        Log.Verbose("Received TCP packet with ID {0:X}", id);

                    if (!HasStartedHandshake && id != 0x3D)
                        return;

                    if (!HasStartedHandshake)
                    {
                        HandshakeRequest handshakeRequest = reader.ReadPacket<HandshakeRequest>();
                        if (handshakeRequest.Name?.Length > 25)
                            handshakeRequest.Name = handshakeRequest.Name.Substring(0, 25);

                        Name = handshakeRequest.Name?.Trim();

                        Log.Information("{0} ({1} - {2}) is attempting to connect ({3}).", handshakeRequest.Name, handshakeRequest.Guid, TcpClient.Client.RemoteEndPoint, handshakeRequest.RequestedCar);

                        List<string> cspFeatures;
                        if (!string.IsNullOrEmpty(handshakeRequest.Features))
                        {
                            Log.Debug("{0} supports extra CSP features: {1}", handshakeRequest.Name, handshakeRequest.Features);
                            cspFeatures = handshakeRequest.Features.Split(',').ToList();
                        }
                        else
                        {
                            cspFeatures = new List<string>();
                        }

                        if (id != 0x3D || handshakeRequest.ClientVersion != 202)
                            SendPacket(new UnsupportedProtocolResponse());
                        else if (Server.IsGuidBlacklisted(handshakeRequest.Guid))
                            SendPacket(new BlacklistedResponse());
                        else if (Server.Configuration.Password?.Length > 0 && handshakeRequest.Password != Server.Configuration.Password && handshakeRequest.Password != Server.Configuration.AdminPassword)
                            SendPacket(new WrongPasswordResponse());
                        else if (!Server.CurrentSession.Configuration.IsOpen)
                            SendPacket(new SessionClosedResponse());
                        else if ((Server.Configuration.Extra.EnableWeatherFx && !cspFeatures.Contains("WEATHERFX_V1"))
                                 || (Server.Configuration.Extra.UseSteamAuth && !cspFeatures.Contains("STEAM_TICKET")))
                            SendPacket(new AuthFailedResponse("Content Manager version not supported. Please update Content Manager to v0.8.2329.38887 or above."));
                        else if (Server.Configuration.Extra.UseSteamAuth && !await Server.Steam.ValidateSessionTicketAsync(handshakeRequest.SessionTicket, handshakeRequest.Guid, this))
                            SendPacket(new AuthFailedResponse("Steam authentication failed."));
                        else if (string.IsNullOrEmpty(handshakeRequest.Guid) || !(handshakeRequest.Guid?.Length >= 6))
                            SendPacket(new AuthFailedResponse("Invalid Guid."));
                        else if (!await Server.TrySecureSlotAsync(this, handshakeRequest))
                            SendPacket(new NoSlotsAvailableResponse());
                        else
                        {
                            if (EntryCar == null)
                                throw new InvalidOperationException("No EntryCar set even though handshake started");
                            
                            var args = new ClientHandshakeEventArgs
                            {
                                HandshakeRequest = handshakeRequest
                            };
                            HandshakeStarted?.Invoke(this, args);

                            if (args.Cancel)
                            {
                                if (args.CancelType == ClientHandshakeEventArgs.CancelTypeEnum.Blacklisted)
                                    SendPacket(new BlacklistedResponse());
                                else if (args.CancelType == ClientHandshakeEventArgs.CancelTypeEnum.AuthFailed)
                                    SendPacket(new AuthFailedResponse(args.AuthFailedReason ?? "No reason specified"));

                                return;
                            }

                            EntryCar.SetActive();
                            Team = handshakeRequest.Team;
                            NationCode = handshakeRequest.Nation;
                            Guid = handshakeRequest.Guid;

                            // Gracefully despawn AI cars
                            EntryCar.SetAiOverbooking(0);

                            if (handshakeRequest.Password == Server.Configuration.AdminPassword)
                                IsAdministrator = true;

                            Log.Information("{0} ({1}, {2} ({3})) has connected.", Name, Guid, SessionId, EntryCar.Model + "-" + EntryCar.Skin);

                            ACServerConfiguration cfg = Server.Configuration;
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
                                SunAngle = WeatherUtils.SunAngleFromSeconds((float)TimeZoneInfo.ConvertTimeFromUtc(Server.CurrentDateTime, Server.TimeZone).TimeOfDay.TotalSeconds),
                                TrackConfig = cfg.TrackConfig,
                                TrackName = cfg.Track,
                                TyreConsumptionRate = cfg.TyreConsumptionRate,
                                UdpPort = cfg.UdpPort,
                                CurrentSession = Server.CurrentSession,
                                ChecksumCount = (byte)Server.TrackChecksums.Count,
                                ChecksumPaths = Server.TrackChecksums.Keys,
                                CurrentTime = Server.CurrentTime,
                                LegalTyres = cfg.LegalTyres,
                                RandomSeed = 123,
                                SessionCount = (byte)cfg.Sessions.Count,
                                Sessions = cfg.Sessions,
                                SpawnPosition = SessionId,
                                TrackGrip = Math.Clamp(cfg.DynamicTrack.Enabled ? cfg.DynamicTrack.BaseGrip + (cfg.DynamicTrack.GripPerLap * cfg.DynamicTrack.TotalLapCount) : 1, 0, 1),
                                MaxContactsPerKm = cfg.MaxContactsPerKm
                            };

                            HasStartedHandshake = true;
                            SendPacket(handshakeResponse);

                            _ = Task.Delay(TimeSpan.FromMinutes(10)).ContinueWith(async t =>
                            {
                                if (EntryCar.Client == this && IsConnected && !HasSentFirstUpdate)
                                {
                                    Log.Information("{0} has taken over 10 minutes to spawn in and will be disconnected.", Name);
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
                            Log.Verbose("Received extended TCP packet with ID {0:X}", id);

                            if (id == 0x00)
                                OnSpectateCar(reader);
                            else if (id == 0x03)
                                OnCspClientMessage(reader);
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
                Log.Error(ex, "Error receiving TCP packet from {0}.", Name);
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
                if (evt.Type == ClientEvent.ClientEventType.PlayerCollision)
                {
                    var targetCar = Server.EntryCars[evt.TargetSessionId];

                    if (targetCar.AiControlled)
                    {
                        var targetAiState = targetCar.GetClosestAiState(EntryCar.Status.Position);
                        if (targetAiState.distanceSquared < 25 * 25)
                        {
                            targetAiState.aiState.StopForCollision();
                        }
                    }
                }
            }
        }

        private void OnSpectateCar(PacketReader reader)
        {
            SpectateCar spectatePacket = reader.ReadPacket<SpectateCar>();
            EntryCar.TargetCar = spectatePacket.SessionId != SessionId ? Server.EntryCars[spectatePacket.SessionId] : null;
        }

        private void OnCspClientMessage(PacketReader reader)
        {
            CSPClientMessageType packetType = (CSPClientMessageType)reader.Read<ushort>();
            if (packetType == CSPClientMessageType.LuaMessage)
            {
                int luaPacketType = reader.Read<int>();

                if (Server.CSPLuaMessageTypes.TryGetValue(luaPacketType, out var typeRegistration))
                {
                    var packet = typeRegistration.FactoryMethod();
                    packet.FromReader(reader);

                    typeRegistration.Handler(this, packet);
                }
                else
                {
                    CSPClientMessage clientMessage = reader.ReadPacket<CSPClientMessage>();
                    clientMessage.Type = packetType;
                    clientMessage.LuaType = luaPacketType;
                    clientMessage.SessionId = SessionId;

                    Log.Debug("Unknown CSP lua client message with type 0x{0:X} received, data {1}", clientMessage.LuaType, Convert.ToHexString(clientMessage.Data));
                    Server.BroadcastPacket(clientMessage);
                }
            }
            else
            {
                CSPClientMessage clientMessage = reader.ReadPacket<CSPClientMessage>();
                clientMessage.Type = packetType;
                clientMessage.SessionId = SessionId;

                Log.Debug("Unknown CSP client message with type {0} received, data {1}", clientMessage.Type, Convert.ToHexString(clientMessage.Data));
                Server.BroadcastPacket(clientMessage);
            }
        }

        private async ValueTask OnChecksumAsync(PacketReader reader)
        {
            bool passedChecksum = false;
            byte[] fullChecksum = new byte[16 * (Server.TrackChecksums.Count + 1)];
            if (reader.Buffer.Length == fullChecksum.Length + 1)
            {
                reader.ReadBytes(fullChecksum);
                passedChecksum = !Server.CarChecksums.TryGetValue(EntryCar.Model, out byte[]? modelChecksum) || fullChecksum.AsSpan().Slice(fullChecksum.Length - 16).SequenceEqual(modelChecksum);

                KeyValuePair<string, byte[]>[] allChecksums = Server.TrackChecksums.ToArray();
                for (int i = 0; i < allChecksums.Length; i++)
                    if (!allChecksums[i].Value.AsSpan().SequenceEqual(fullChecksum.AsSpan().Slice(i * 16, 16)))
                    {
                        Log.Information("{0} failed checksum for file {1}.", Name, allChecksums[i].Key);
                        passedChecksum = false;
                        break;
                    }
            }

            HasPassedChecksum = passedChecksum;
            if (!passedChecksum)
            {
                ChecksumFailed?.Invoke(this, EventArgs.Empty);
                await Server.KickAsync(this, KickReason.ChecksumFailed, $"{Name} failed the checksum check and has been kicked.", false);
            }
            else
            {
                ChecksumPassed?.Invoke(this, EventArgs.Empty);

                Server.BroadcastPacket(new CarConnected
                {
                    SessionId = SessionId,
                    Name = Name,
                    Nation = NationCode
                }, this);
            }
        }

        private void OnChat(PacketReader reader)
        {
            if (Environment.TickCount64 - LastChatTime < 1000)
                return;
            LastChatTime = Environment.TickCount64;

            if (Server.Configuration.Extra.AfkKickBehavior == AfkKickBehavior.PlayerInput)
            {
                EntryCar?.SetActive();
            }

            ChatMessage chatMessage = reader.ReadPacket<ChatMessage>();
            chatMessage.SessionId = SessionId;

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

            Server.BroadcastPacket(new TyreCompoundUpdate
            {
                CompoundName = compoundChangeRequest.CompoundName,
                SessionId = SessionId
            });
        }

        private void OnP2PUpdate(PacketReader reader)
        {
            P2PUpdateRequest p2pUpdateRequest = reader.ReadPacket<P2PUpdateRequest>();
            if (p2pUpdateRequest.P2PCount == -1)
                SendPacket(new P2PUpdate
                {
                    Active = false,
                    P2PCount = EntryCar.Status.P2PCount,
                    SessionId = SessionId
                });
            else
                Server.BroadcastPacket(new P2PUpdate
                {
                    Active = EntryCar.Status.P2PActive,
                    P2PCount = EntryCar.Status.P2PCount,
                    SessionId = SessionId
                });
        }

        private void OnCarListRequest(PacketReader reader)
        {
            CarListRequest carListRequest = reader.ReadPacket<CarListRequest>();

            List<EntryCar> carsInPage = Server.EntryCars.Skip(carListRequest.PageIndex).Take(10).ToList();
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

            //Server.Configuration.DynamicTrack.TotalLapCount++; // TODO reset at some point
            if (OnLapCompleted(lapPacket))
            {
                Server.SendLapCompletedMessage(SessionId, lapPacket.LapTime, lapPacket.Cuts);
            }
        }

        private bool OnLapCompleted(LapCompletedIncoming lap)
        {
            int timestamp = Server.CurrentTime;

            var entryCarResult = Server.CurrentSession.Results?[SessionId] ?? throw new InvalidOperationException("Current session does not have results set");

            if (entryCarResult.HasCompletedLastLap)
            {
                Log.Debug("Lap rejected by {0}, already finished", Name);
                return false;
            }

            if (Server.CurrentSession.Configuration.Type == SessionType.Race && entryCarResult.NumLaps >= Server.CurrentSession.Configuration.Laps && !Server.CurrentSession.Configuration.IsTimedRace)
            {
                Log.Debug("Lap rejected by {0}, race over", Name);
                return false;
            }

            Log.Information("Lap completed by {0}, {1} cuts, laptime {2}", Name, lap.Cuts, lap.LapTime);

            // TODO unfuck all of this

            if (Server.CurrentSession.Configuration.Type == SessionType.Race || lap.Cuts == 0)
            {
                entryCarResult.LastLap = lap.LapTime;
                if (lap.LapTime < entryCarResult.BestLap)
                {
                    entryCarResult.BestLap = lap.LapTime;
                }

                entryCarResult.NumLaps++;
                if (entryCarResult.NumLaps > Server.CurrentSession.LeaderLapCount)
                {
                    Server.CurrentSession.LeaderLapCount = entryCarResult.NumLaps;
                }

                entryCarResult.TotalTime = Server.CurrentSession.SessionTimeTicks - (EntryCar.Ping / 2);

                if (Server.CurrentSession.SessionOverFlag)
                {
                    if (Server.CurrentSession.Configuration.Type == SessionType.Race && Server.CurrentSession.Configuration.IsTimedRace)
                    {
                        if (Server.Configuration.HasExtraLap)
                        {
                            if (entryCarResult.NumLaps <= Server.CurrentSession.LeaderLapCount)
                            {
                                entryCarResult.HasCompletedLastLap = Server.CurrentSession.LeaderHasCompletedLastLap;
                            }
                            else if (Server.CurrentSession.TargetLap > 0)
                            {
                                if (entryCarResult.NumLaps >= Server.CurrentSession.TargetLap)
                                {
                                    Server.CurrentSession.LeaderHasCompletedLastLap = true;
                                    entryCarResult.HasCompletedLastLap = true;
                                }
                            }
                            else
                            {
                                Server.CurrentSession.TargetLap = entryCarResult.NumLaps + 1;
                            }
                        }
                        else if (entryCarResult.NumLaps <= Server.CurrentSession.LeaderLapCount)
                        {
                            entryCarResult.HasCompletedLastLap = Server.CurrentSession.LeaderHasCompletedLastLap;
                        }
                        else
                        {
                            Server.CurrentSession.LeaderHasCompletedLastLap = true;
                            entryCarResult.HasCompletedLastLap = true;
                        }
                    }
                    else
                    {
                        entryCarResult.HasCompletedLastLap = true;
                    }
                }

                if (Server.CurrentSession.Configuration.Type != SessionType.Race)
                {
                    if (Server.CurrentSession.EndTime != 0)
                    {
                        entryCarResult.HasCompletedLastLap = true;
                    }
                }
                else if (Server.CurrentSession.Configuration.IsTimedRace)
                {
                    if (Server.CurrentSession.LeaderHasCompletedLastLap && Server.CurrentSession.EndTime == 0)
                    {
                        Server.CurrentSession.EndTime = timestamp;
                    }
                }
                else if (entryCarResult.NumLaps != Server.CurrentSession.Configuration.Laps)
                {
                    if (Server.CurrentSession.EndTime != 0)
                    {
                        entryCarResult.HasCompletedLastLap = true;
                    }
                }
                else if (!entryCarResult.HasCompletedLastLap)
                {
                    entryCarResult.HasCompletedLastLap = true;
                    if (Server.CurrentSession.EndTime == 0)
                    {
                        Server.CurrentSession.EndTime = timestamp;
                    }
                }
                else if (Server.CurrentSession.EndTime != 0)
                {
                    entryCarResult.HasCompletedLastLap = true;
                }

                return true;
            }

            if (Server.CurrentSession.EndTime == 0)
                return true;

            entryCarResult.HasCompletedLastLap = true;
            return false;
        }

        internal void SendFirstUpdate()
        {
            if (HasSentFirstUpdate)
                return;

            TcpClient.ReceiveTimeout = 0;
            EntryCar.LastPongTime = Server.CurrentTime;
            HasSentFirstUpdate = true;

            List<EntryCar> connectedCars = Server.EntryCars.Where(c => c.Client != null || c.AiControlled).ToList();

            if (!string.IsNullOrEmpty(Server.CSPServerExtraOptions.EncodedWelcomeMessage))
                SendPacket(new WelcomeMessage { Message = Server.CSPServerExtraOptions.EncodedWelcomeMessage });

            SendPacket(new DriverInfoUpdate { ConnectedCars = connectedCars });
            Server.WeatherImplementation.SendWeather(this);

            foreach (EntryCar car in connectedCars)
            {
                SendPacket(new MandatoryPitUpdate { MandatoryPit = car.Status.MandatoryPit, SessionId = car.SessionId });
                if (car != EntryCar)
                    SendPacket(new TyreCompoundUpdate { SessionId = car.SessionId, CompoundName = car.Status.CurrentTyreCompound });

                if (Server.Configuration.Extra.AiParams.HideAiCars)
                {
                    SendPacket(new CSPCarVisibilityUpdate
                    {
                        SessionId = car.SessionId,
                        Visible = car.AiControlled ? CSPCarVisibility.Invisible : CSPCarVisibility.Visible
                    });
                }
            }

            Server.SendLapCompletedMessage(255, 0, 0, this);

            _ = Task.Delay(40000).ContinueWith(async t =>
            {
                if (!HasPassedChecksum && IsConnected)
                {
                    await Server.KickAsync(this, KickReason.ChecksumFailed, $"{Name} did not send the requested checksums.", false);
                }
            });
        }

        internal bool TryAssociateUdp(IPEndPoint endpoint)
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
                    Log.Debug("Disconnecting {0} ({1}).", Name, TcpClient?.Client?.RemoteEndPoint);
                    Disconnecting?.Invoke(this, EventArgs.Empty);
                }

                OutgoingPacketChannel.Writer.TryComplete();
                await Task.WhenAny(Task.Delay(2000), SendLoopTask);

                try
                {
                    DisconnectTokenSource.Cancel();
                    DisconnectTokenSource.Dispose();
                }
                catch (ObjectDisposedException) { }

                if (IsConnected)
                    await Server.DisconnectClientAsync(this);

                TcpClient?.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error disconnecting {0}", Name);
            }
        }
    }
}
