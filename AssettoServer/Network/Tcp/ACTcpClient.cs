﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Serilog;
using AssettoServer.Server.Ai;
using AssettoServer.Network.Packets;
using AssettoServer.Network.Packets.Incoming;
using AssettoServer.Network.Packets.Outgoing;
using AssettoServer.Network.Packets.Outgoing.Handshake;
using AssettoServer.Network.Packets.Shared;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using Qmmands;
using Steamworks;

namespace AssettoServer.Network.Tcp
{
    public class ACTcpClient
    {
        public ACServer Server { get; }
        public byte SessionId { get; set; }
        public string Name { get; private set; }
        public string Team { get; private set; }
        public string NationCode { get; private set; }
        public bool IsAdministrator { get; internal set; }
        public string Guid { get; internal set; }
        public bool IsChatLogEnabled { get; set; }

        internal TcpClient TcpClient { get; }
        internal NetworkStream TcpStream { get; }
        internal EntryCar EntryCar { get; set; }
        internal bool HasSentFirstUpdate { get; private set; }
        internal bool HasStartedHandshake { get; private set; }
        internal bool HasPassedChecksum { get; private set; }
        internal bool IsConnected { get; set; }

        internal IPEndPoint UdpEndpoint { get; private set; }
        internal bool HasAssociatedUdp { get; private set; }

        private Memory<byte> TcpSendBuffer { get; }
        private SemaphoreSlim TcpSendSemaphore { get; }
        private Channel<IOutgoingNetworkPacket> OutgoingPacketChannel { get; }
        private CancellationTokenSource DisconnectTokenSource { get; }
        private Task SendLoopTask { get; set; }
        private long LastChatTime { get; set; }

        internal ACTcpClient(ACServer server, TcpClient tcpClient)
        {
            Server = server;

            TcpClient = tcpClient;
            tcpClient.ReceiveTimeout = (int)TimeSpan.FromMinutes(5).TotalMilliseconds;

            TcpStream = tcpClient.GetStream();

            TcpSendBuffer = new byte[8192 + ((server.Configuration.WelcomeMessage?.Length ?? 0) * 4) + 2];
            TcpSendSemaphore = new SemaphoreSlim(1, 1);
            OutgoingPacketChannel = Channel.CreateBounded<IOutgoingNetworkPacket>(256);
            DisconnectTokenSource = new CancellationTokenSource();
        }

        internal Task StartAsync()
        {
            SendLoopTask = Task.Factory.StartNew(SendLoopAsync, TaskCreationOptions.LongRunning);
            _ = Task.Factory.StartNew(ReceiveLoopAsync, TaskCreationOptions.LongRunning);

            return Task.CompletedTask;
        }

        internal void SendPacket<TPacket>(TPacket packet) where TPacket : IOutgoingNetworkPacket
        {
            try
            {
                if (!OutgoingPacketChannel.Writer.TryWrite(packet) && !(packet is SunAngleUpdate))
                    Log.Warning("Failed to queue packet {0} for {1}. Perhaps the outgoing packet channel is full?", typeof(TPacket).Name, Name);
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
                byte[] buffer = new byte[2048];
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
            while (!DisconnectTokenSource.IsCancellationRequested)
            {
                IOutgoingNetworkPacket packet = default;

                try
                {
                    packet = await OutgoingPacketChannel.Reader.ReadAsync(DisconnectTokenSource.Token);
                    if (!(packet is SunAngleUpdate))
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
                catch (ChannelClosedException) { }
                catch (ObjectDisposedException) { }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error sending {0} to {1}.", packet?.GetType().Name ?? "(no packet)", Name);
                    _ = DisconnectAsync();
                }
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

                        Log.Information("{0} ({1}) is attempting to connect ({2}).", handshakeRequest.Name, handshakeRequest.Guid, handshakeRequest.RequestedCar);
                        if(!string.IsNullOrEmpty(handshakeRequest.Features))
                        {
                            Log.Debug("{0} supports extra CSP features: {1}", handshakeRequest.Name, handshakeRequest.Features);
                        }

                        if (id != 0x3D || handshakeRequest.ClientVersion != 202)
                            SendPacket(new UnsupportedProtocolResponse());
                        else if (Server.IsGuidBlacklisted(handshakeRequest.Guid))
                            SendPacket(new BlacklistedResponse());
                        else if (Server.Configuration.Password?.Length > 0 && handshakeRequest.Password != Server.Configuration.Password)
                            SendPacket(new WrongPasswordResponse());
                        else if (!Server.CurrentSession.IsOpen)
                            SendPacket(new SessionClosedResponse());
                        //else if (handshakeRequest.Password.Length > 0 && handshakeRequest.Password != Server.Configuration.AdminPassword)
                        //    await SendPacketAsync(new AuthFailedResponse("Incorrect admin password."));
                        else if (Server.Configuration.Extra.UseSteamAuth && !await ValidateSessionTicketAsync(handshakeRequest.SessionTicket, handshakeRequest.Guid))
                            SendPacket(new AuthFailedResponse("Steam authentication failed."));
                        else if (string.IsNullOrEmpty(handshakeRequest.Guid) || !(handshakeRequest.Guid?.Length >= 6))
                            SendPacket(new AuthFailedResponse("Invalid Guid."));
                        else if (string.IsNullOrEmpty(handshakeRequest.Name) || handshakeRequest.Name == null || handshakeRequest.Name.Length < 1)
                            SendPacket(new AuthFailedResponse("Username is not allowed."));
                        else if (handshakeRequest.Name == "RLD!")
                            SendPacket(new AuthFailedResponse("The name \"RLD!\" is not allowed."));
                        else if (!await Server.TrySecureSlotAsync(this, handshakeRequest))
                            SendPacket(new NoSlotsAvailableResponse());
                        else
                        {
                            if (Name.Equals("player", StringComparison.OrdinalIgnoreCase))
                                Name += handshakeRequest.Guid[^6..];

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
                                SunAngle = cfg.SunAngle,
                                TrackConfig = cfg.TrackConfig,
                                TrackName = cfg.Track,
                                TyreConsumptionRate = cfg.TyreConsumptionRate,
                                UdpPort = (ushort)cfg.UdpPort,
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
                            await OnChatAsync(reader);
                        else if (id == 0x44)
                            await OnChecksumAsync(reader);
                        else if (id == 0xAB)
                        {
                            id = reader.Read<byte>();
                            Log.Verbose("Received extended TCP packet with ID {0:X}", id);

                            if (id == 0x00)
                                OnSpectateCar(reader);
                        }
                        //else if (id == 0x82)
                        //    await OnClientEvent(reader);
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

        private async ValueTask<bool> ValidateSessionTicketAsync(byte[] sessionTicket, string guid)
        {
            if (sessionTicket == null || !ulong.TryParse(guid, out ulong steamId))
                return false;

            TaskCompletionSource<bool> taskCompletionSource = new TaskCompletionSource<bool>();
            void tickedValidateResponse(SteamId arg1, SteamId arg2, AuthResponse arg3)
            {
                if (arg1 == steamId)
                {
                    if (arg3 != AuthResponse.OK)
                        Log.Information("Steam auth ticket verification failed ({0}) for {1}.", arg3, Name);
                    else
                        Log.Information("Steam auth ticket verification succeeded for {0}.", Name);

                    taskCompletionSource.SetResult(arg3 == AuthResponse.OK);
                }
            }

            bool validated = false;

            SteamServer.OnValidateAuthTicketResponse += tickedValidateResponse;
            Task timeoutTask = Task.Delay(2000);
            Task beginAuthTask = Task.Run(() =>
            {
                if (!SteamServer.BeginAuthSession(sessionTicket, steamId))
                    taskCompletionSource.SetResult(false);
            });

            Task finishedTask = await Task.WhenAny(timeoutTask, taskCompletionSource.Task);

            if (finishedTask == timeoutTask)
            {
                Log.Warning("Steam auth ticket verification timed out for {0}.", Name);
            }
            else
            {
                validated = await taskCompletionSource.Task;
            }

            SteamServer.OnValidateAuthTicketResponse -= tickedValidateResponse;
            return validated;
        }

        private void OnClientEvent(PacketReader reader)
        {
            ClientEvent clientEvent = reader.ReadPacket<ClientEvent>();
            if (clientEvent.Type == 0xC)
            {
                clientEvent.SessionId = SessionId;
                Server.BroadcastPacket(clientEvent);
            }
        }

        private void OnSpectateCar(PacketReader reader)
        {
            SpectateCar spectatePacket = reader.ReadPacket<SpectateCar>();
            if ((spectatePacket.SessionId != SessionId || spectatePacket.CameraMode == 2) && Server.ConnectedCars.TryGetValue(spectatePacket.SessionId, out EntryCar targetCar) && targetCar.Client != null)
                EntryCar.TargetCar = targetCar;
            else
                EntryCar.TargetCar = null;

        }

        private async ValueTask OnChecksumAsync(PacketReader reader)
        {
            bool passedChecksum = false;
            byte[] fullChecksum = new byte[16 * (Server.TrackChecksums.Count + 1)];
            if (reader.Buffer.Length == fullChecksum.Length + 1)
            {
                reader.ReadBytes(fullChecksum);
                passedChecksum = Server.CarChecksums.TryGetValue(EntryCar.Model, out byte[] modelChecksum) && fullChecksum.AsSpan().Slice(fullChecksum.Length - 16).SequenceEqual(modelChecksum);

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
                await Server.KickAsync(this, KickReason.ChecksumFailed, $"{Name} failed the checksum check and has been kicked.", false);
            }
            else
            {
                if (Server.Configuration.Extra.EnableAi)
                {
                    EntryCar.SetAiControl(false);
                    Server.AiBehavior.AdjustOverbooking();
                }

                Server.BroadcastPacket(new CarConnected
                {
                    SessionId = SessionId,
                    Name = Name,
                    Nation = NationCode
                }, this);
            }
        }

        private async Task OnChatAsync(PacketReader reader)
        {
            if (Environment.TickCount64 - LastChatTime < 1000)
                return;
            LastChatTime = Environment.TickCount64;

            EntryCar?.SetActive();
            ChatMessage chatMessage = reader.ReadPacket<ChatMessage>();
            chatMessage.SessionId = SessionId;

            Log.Information("{0} ({1}): {2}", Name, SessionId, chatMessage.Message);

            if (!CommandUtilities.HasPrefix(chatMessage.Message, '/', out string commandStr))
            {
                Server.BroadcastPacket(chatMessage);
                Server.Discord.SendChatMessage(Name, chatMessage.Message);
            }
            else
            {
                chatMessage.Message = commandStr;
                await Server.ProcessCommandAsync(this, chatMessage);
            }
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

        internal void SendCurrentSession()
        {
            ACServerConfiguration cfg = Server.Configuration;
            SendPacket(new CurrentSessionUpdate
            {
                CurrentSession = Server.CurrentSession,
                ConnectedCars = Server.EntryCars,
                TargetCar = EntryCar,
                TrackGrip = Math.Clamp(cfg.DynamicTrack.Enabled ? cfg.DynamicTrack.BaseGrip + (cfg.DynamicTrack.GripPerLap * cfg.DynamicTrack.TotalLapCount) : 1, 0, 1)
            });
        }

        internal void SendFirstUpdate()
        {
            if (HasSentFirstUpdate)
                return;

            TcpClient.ReceiveTimeout = 0;
            EntryCar.LastPongTime = Server.CurrentTime;
            HasSentFirstUpdate = true;

            ACServerConfiguration cfg = Server.Configuration;
            List<EntryCar> connectedCars = Server.EntryCars.Where(c => c.Client != null || c.AiControlled).ToList();

            if (cfg.WelcomeMessage.Length > 0)
                SendPacket(new WelcomeMessage { Message = cfg.WelcomeMessage });

            SendPacket(new DriverInfoUpdate { ConnectedCars = connectedCars });
            Server.SendCurrentWeather(this);

            foreach (EntryCar car in connectedCars)
            {
                SendPacket(new MandatoryPitUpdate { MandatoryPit = car.Status.MandatoryPit, SessionId = car.SessionId });
                if (car != EntryCar)
                    SendPacket(new TyreCompoundUpdate { SessionId = car.SessionId, CompoundName = car.Status.CurrentTyreCompound });
            }

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
                if (!DisconnectTokenSource.IsCancellationRequested)
                    Log.Debug("Disconnecting {0} ({1}).", Name, TcpClient?.Client?.RemoteEndPoint);

                await Task.WhenAny(Task.Delay(2000), SendLoopTask);
                OutgoingPacketChannel.Writer.TryComplete();
                DisconnectTokenSource.Cancel();

                if (IsConnected)
                    await Server.DisconnectClientAsync(this);

                CloseConnection();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error disconnecting {0}.", Name);
            }
        }

        private void CloseConnection()
        {

            try
            {
                TcpStream.Dispose();
                TcpClient.Dispose();
            }
            catch { }
        }
    }
}
