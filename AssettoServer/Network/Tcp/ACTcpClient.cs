using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AssettoServer.Network.ClientMessages;
using AssettoServer.Network.Udp;
using AssettoServer.Server;
using AssettoServer.Server.Blacklist;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.OpenSlotFilters;
using AssettoServer.Server.Weather;
using AssettoServer.Shared.Model;
using AssettoServer.Shared.Network.Packets;
using AssettoServer.Shared.Network.Packets.Incoming;
using AssettoServer.Shared.Network.Packets.Outgoing;
using AssettoServer.Shared.Network.Packets.Outgoing.Handshake;
using AssettoServer.Shared.Network.Packets.Shared;
using AssettoServer.Shared.Weather;
using AssettoServer.Utils;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace AssettoServer.Network.Tcp;

public class ACTcpClient : IClient
{
    private ACUdpServer UdpServer { get; }
    public ILogger Logger { get; }
    public byte SessionId { get; set; }
    public string? Name { get; set; }
    public string? Team { get; private set; }
    public string? NationCode { get; private set; }
    public bool IsAdministrator { get; internal set; }
    public ulong Guid { get; internal set; }
    public string HashedGuid { get; private set; } = "";
    public ulong? OwnerGuid { get; internal set; }
    public EntryCar EntryCar { get; internal set; } = null!;
    public bool IsDisconnectRequested => _disconnectRequested == 1;
    [MemberNotNullWhen(true, nameof(Name), nameof(Team), nameof(NationCode))]
    public bool HasSentFirstUpdate { get; private set; }
    public bool IsConnected { get; set; }
    public TcpClient TcpClient { get; }

    private NetworkStream TcpStream { get; }
    [MemberNotNullWhen(true, nameof(Name), nameof(Team), nameof(NationCode))]
    public bool HasStartedHandshake { get; private set; }
    public ChecksumStatus ChecksumStatus { get; private set; } = ChecksumStatus.Pending;
    public byte[]? CarChecksum { get; private set; }
    public int SecurityLevel { get; set; }
    public ulong? HardwareIdentifier { get; set; }
    public InputMethod InputMethod { get; set; }

    internal SocketAddress? UdpEndpoint { get; private set; }
    public bool HasUdpEndpoint => UdpEndpoint != null;
    public bool SupportsCSPCustomUpdate { get; private set; }
    public int? CSPVersion { get; private set; }
    internal string ApiKey { get; }

    private static ThreadLocal<byte[]> UdpSendBuffer { get; } = new(() => GC.AllocateArray<byte>(1500, true));
    private byte[] TcpSendBuffer { get; }
    private Channel<IOutgoingNetworkPacket> OutgoingPacketChannel { get; }
    private CancellationTokenSource DisconnectTokenSource { get; }
    private Task SendLoopTask { get; set; } = null!;
    private long LastChatTime { get; set; }
    private int _disconnectRequested = 0;

    private readonly WeatherManager _weatherManager;
    private readonly SessionManager _sessionManager;
    private readonly EntryCarManager _entryCarManager;
    private readonly ACServerConfiguration _configuration;
    private readonly IBlacklistService _blacklist;
    private readonly ChecksumManager _checksumManager;
    private readonly CSPFeatureManager _cspFeatureManager;
    private readonly CSPServerExtraOptions _cspServerExtraOptions;
    private readonly OpenSlotFilterChain _openSlotFilter;
    private readonly CSPClientMessageHandler _clientMessageHandler;
    private readonly VoteManager _voteManager;

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
    ///  Fires when a slot has been secured for a player and the handshake response is about to be sent.
    /// </summary>
    public event EventHandler<ACTcpClient, HandshakeAcceptedEventArgs>? HandshakeAccepted;

    /// <summary>
    /// Fires when a client has sent the first position update and is visible to other players.
    /// </summary>
    public event EventHandler<ACTcpClient, EventArgs>? FirstUpdateSent;

    /// <summary>
    /// Fires when a client collided with something. TargetCar will be null for environment collisions.
    /// There are up to 5 seconds delay before a collision is reported to the server.
    /// </summary>
    public event EventHandler<ACTcpClient, CollisionEventArgs>? Collision;
    
    /// <summary>
    /// Fires when a client has changed tyre compound
    /// </summary>
    public event EventHandler<ACTcpClient, TyreCompoundChangeEventArgs>? TyreCompoundChange;
    
    /// <summary>
    /// Fires when a client has received damage
    /// </summary>
    public event EventHandler<ACTcpClient, DamageEventArgs>? Damage;
    
    /// <summary>
    /// Fires when a client has used P2P
    /// </summary>
    public event EventHandler<ACTcpClient, Push2PassEventArgs>? Push2Pass;

    /// <summary>
    /// Fires when a client received a penalty.
    /// </summary>
    public event EventHandler<ACTcpClient, EventArgs>? JumpStartPenalty;

    /// <summary>
    /// Fires when a client has completed a lap
    /// </summary>
    public event EventHandler<ACTcpClient, LapCompletedEventArgs>? LapCompleted;
    
    /// <summary>
    /// Fires when a client has completed a sector
    /// </summary>
    public event EventHandler<ACTcpClient, SectorSplitEventArgs>? SectorSplit;

    /// <summary>
    /// Fires before sending the car list response
    /// </summary>
    public event EventHandler<ACTcpClient, CarListResponseSendingEventArgs>? CarListResponseSending;
    
    /// <summary>
    /// Fires when a player has authorized for admin permissions.
    /// </summary>
    public event EventHandler<ACTcpClient, EventArgs>? LoggedInAsAdministrator;
    
    /// <summary>
    /// Called when all Lua server scripts are loaded on the client. Warning: This can be called multiple times if scripts are reloaded!
    /// </summary>
    public event EventHandler<ACTcpClient, EventArgs>? LuaReady;

    private class ACTcpClientLogEventEnricher : ILogEventEnricher
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
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ClientIpAddress", endpoint.Address));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ClientPort", endpoint.Port));
            if (_client.HardwareIdentifier.HasValue)
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ClientHWID", _client.HardwareIdentifier.Value));
            }
        }
    }

    public ACTcpClient(
        ACUdpServer udpServer,
        TcpClient tcpClient,
        SessionManager sessionManager,
        WeatherManager weatherManager,
        ACServerConfiguration configuration,
        EntryCarManager entryCarManager,
        IBlacklistService blacklist,
        ChecksumManager checksumManager,
        CSPFeatureManager cspFeatureManager,
        CSPServerExtraOptions cspServerExtraOptions,
        OpenSlotFilterChain openSlotFilter,
        CSPClientMessageHandler clientMessageHandler,
        VoteManager voteManager)
    {
        UdpServer = udpServer;
        Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.With(new ACTcpClientLogEventEnricher(this))
            .WriteTo.Logger(Log.Logger)
            .CreateLogger();

        TcpClient = tcpClient;
        _sessionManager = sessionManager;
        _weatherManager = weatherManager;
        _configuration = configuration;
        _entryCarManager = entryCarManager;
        _blacklist = blacklist;
        _checksumManager = checksumManager;
        _cspFeatureManager = cspFeatureManager;
        _cspServerExtraOptions = cspServerExtraOptions;
        _openSlotFilter = openSlotFilter;
        _clientMessageHandler = clientMessageHandler;
        _voteManager = voteManager;

        tcpClient.ReceiveTimeout = (int)TimeSpan.FromMinutes(5).TotalMilliseconds;
        tcpClient.SendTimeout = (int)TimeSpan.FromSeconds(30).TotalMilliseconds;
        tcpClient.LingerState = new LingerOption(true, 2);
        tcpClient.NoDelay = true;

        TcpStream = tcpClient.GetStream();

        TcpSendBuffer = GC.AllocateArray<byte>(ushort.MaxValue + 2, true);
        OutgoingPacketChannel = Channel.CreateBounded<IOutgoingNetworkPacket>(256);
        DisconnectTokenSource = new CancellationTokenSource();

        ApiKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        LuaReady += OnLuaReady;
    }

    private void OnLuaReady(ACTcpClient sender, EventArgs args)
    {
        SendPacket(new ApiKeyPacket { Key = ApiKey });
        
        var connectedCars = _entryCarManager.EntryCars.Where(c => c.Client != null || c.AiControlled).ToList();
        foreach (var car in connectedCars)
        {
            if (!car.EnableCollisions)
            {
                SendPacket(new CollisionUpdatePacket { SessionId = car.SessionId, Enabled = car.EnableCollisions });
            }
        }
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

    public void SendPacketUdp<TPacket>(in TPacket packet) where TPacket : IOutgoingNetworkPacket, allows ref struct
    {
        if (UdpEndpoint == null) return;

        try
        {
            byte[] buffer = UdpSendBuffer.Value!;
            PacketWriter writer = new PacketWriter(buffer);
            int bytesWritten = writer.WritePacket(in packet);

            UdpServer.Send(UdpEndpoint, buffer, 0, bytesWritten);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error sending {PacketName} to {ClientName}", typeof(TPacket).Name, Name);
            _ = DisconnectAsync();
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
                    else if (packet is ChatMessage { SessionId: 255 } chatMessage)
                        Logger.Verbose("Sending {PacketName} ({ChatMessage}) to {ClientName}", packet.GetType().Name, chatMessage.Message, Name);
                    else
                        Logger.Verbose("Sending {PacketName} to {ClientName}", packet.GetType().Name, Name);
                }

                if (packet is BatchedPacket batched)
                {
                    const int streamLength = 30000;
                    var streamOffset = TcpSendBuffer.Length - streamLength;
                    using var tempStream = new MemoryStream(TcpSendBuffer, streamOffset, streamLength);
                    var tempBuffer = TcpSendBuffer.AsMemory(0, streamOffset);
                    foreach (var inner in batched.Packets)
                    {
                        var writer = new PacketWriter(tempStream, tempBuffer);
                        writer.WritePacket(inner);
                        await writer.SendAsync(DisconnectTokenSource.Token);
                    }

                    await TcpStream.WriteAsync(TcpSendBuffer.AsMemory(streamOffset, (int)tempStream.Position), DisconnectTokenSource.Token);
                }
                else
                {
                    var writer = new PacketWriter(TcpStream, TcpSendBuffer);
                    writer.WritePacket(packet);
                    await writer.SendAsync(DisconnectTokenSource.Token);
                }
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

                ACServerProtocol id = (ACServerProtocol)reader.Read<byte>();

                if (id != ACServerProtocol.ClientEvent)
                    Logger.Verbose("Received TCP packet with ID {PacketId:X}", id);

                if (!HasStartedHandshake && id != ACServerProtocol.RequestNewConnection)
                    return;

                if (!HasStartedHandshake)
                {
                    HandshakeRequest handshakeRequest = reader.ReadPacket<HandshakeRequest>();
                    if (handshakeRequest.Name.Length > 25)
                        handshakeRequest.Name = handshakeRequest.Name.Substring(0, 25);

                    Name = handshakeRequest.Name.Trim();
                    Team = handshakeRequest.Team;
                    NationCode = handshakeRequest.Nation;
                    if (handshakeRequest.Guid != 0)
                        Guid = handshakeRequest.Guid;
                    else if (_configuration.Extra.EnableACProSupport)
                        Guid = handshakeRequest.Guid = GuidFromName(Name);
                    HashedGuid = IdFromGuid(Guid);

                    Logger.Information("{ClientName} ({ClientSteamId} - {ClientIpEndpoint}) is attempting to connect ({CarModel})", handshakeRequest.Name, handshakeRequest.Guid, (IPEndPoint?)TcpClient.Client.RemoteEndPoint, handshakeRequest.RequestedCar);

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

                    AuthFailedResponse? response;
                    if (id != ACServerProtocol.RequestNewConnection || handshakeRequest.ClientVersion != 202)
                        SendPacket(new UnsupportedProtocolResponse());
                    else if (Guid == 0)
                        SendPacket(new AuthFailedResponse("Assetto Corsa Pro is not supported on this server. Consider setting EnableACProSupport to true in extra_cfg.yml"));
                    else if (await _blacklist.IsBlacklistedAsync(Guid))
                        SendPacket(new BlacklistedResponse());
                    else if (_configuration.Server.Password?.Length > 0
                             && handshakeRequest.Password != _configuration.Server.Password
                             && !_configuration.Server.CheckAdminPassword(handshakeRequest.Password))
                        SendPacket(new WrongPasswordResponse());
                    else if (!_sessionManager.IsOpen)
                        SendPacket(new SessionClosedResponse());
                    else if (Name.Length == 0)
                        SendPacket(new AuthFailedResponse("Driver name cannot be empty."));
                    else if (!_cspFeatureManager.ValidateHandshake(cspFeatures))
                        SendPacket(new AuthFailedResponse("Missing CSP features. Please update CSP and/or Content Manager."));
                    else if ((response = await _openSlotFilter.ShouldAcceptConnectionAsync(this, handshakeRequest)).HasValue)
                        SendPacket(response.Value);
                    else if (!await _entryCarManager.TrySecureSlotAsync(this, handshakeRequest))
                        SendPacket(new NoSlotsAvailableResponse());
                    else
                    {
                        if (EntryCar == null)
                            throw new InvalidOperationException("No EntryCar set even though handshake started");

                        EntryCar.SetActive();
                        SupportsCSPCustomUpdate = _configuration.Extra.EnableCustomUpdate && cspFeatures.Contains("CUSTOM_UPDATE");

                        var cspVersionStr = cspFeatures.LastOrDefault("");
                        if (int.TryParse(cspVersionStr, out var cspVersion))
                        {
                            CSPVersion = cspVersion;
                        }

                        // Gracefully despawn AI cars
                        EntryCar.SetAiOverbooking(0);

                        if (_configuration.Server.CheckAdminPassword(handshakeRequest.Password))
                            IsAdministrator = true;

                        Logger.Information("{ClientName} ({ClientSteamId}, {SessionId} ({CarModel}-{CarSkin})) has connected",
                            Name, Guid, SessionId, EntryCar.Model, EntryCar.Skin);

                        var cfg = _configuration.Server;
                        var checksums = _checksumManager.GetChecksumsForHandshake(EntryCar.Model);
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
                            RaceOverTime = cfg.RaceOverTime * 1000,
                            RefreshRateHz = cfg.RefreshRateHz,
                            ResultScreenTime = cfg.ResultScreenTime * 1000,
                            ServerName = cfg.Name,
                            SessionId = SessionId,
                            SunAngle = (float)WeatherUtils.SunAngleFromTicks(_weatherManager.CurrentDateTime.TimeOfDay.TickOfDay),
                            TrackConfig = cfg.TrackConfig,
                            TrackName = cfg.Track,
                            TyreConsumptionRate = cfg.TyreConsumptionRate,
                            UdpPort = cfg.UdpPort,
                            CurrentSession = _sessionManager.CurrentSession.Configuration,
                            SessionTime = _sessionManager.CurrentSession.SessionTimeMilliseconds,
                            ChecksumCount = (byte)checksums.Count,
                            ChecksumPaths = checksums.Select(c => c.Key),
                            CurrentTime = 0, // Ignored by AC
                            LegalTyres = EntryCar.LegalTyres,
                            RandomSeed = _configuration.RandomSeed,
                            SessionCount = (byte)_configuration.Sessions.Count,
                            Sessions = _configuration.Sessions,
                            SpawnPosition = SessionId,
                            TrackGrip = _weatherManager.CurrentWeather.TrackGrip,
                            MaxContactsPerKm = cfg.MaxContactsPerKm
                        };

                        var args = new HandshakeAcceptedEventArgs
                        {
                            HandshakeResponse = handshakeResponse
                        };

                        await HandshakeAccepted.InvokeAsync(this, args);

                        HasStartedHandshake = true;
                        SendPacket(handshakeResponse);

                        _ = Task.Delay(TimeSpan.FromMinutes(_configuration.Extra.PlayerLoadingTimeoutMinutes)).ContinueWith(async _ =>
                        {
                            if (EntryCar.Client == this && IsConnected && !HasSentFirstUpdate)
                            {
                                Logger.Information("{ClientName} has taken too long to spawn in and will be disconnected", Name);
                                await DisconnectAsync();
                            }
                        });
                    }

                    if (!HasStartedHandshake)
                        return;
                }
                else if (HasStartedHandshake)
                {
                    switch (id)
                    {
                        case ACServerProtocol.CleanExitDrive:
                            Logger.Debug("Received clean exit from {ClientName} ({SessionId})", Name, SessionId);
                            return;
                        case ACServerProtocol.P2PUpdate:
                            OnP2PUpdate(reader);
                            break;
                        case ACServerProtocol.CarListRequest:
                            OnCarListRequest(reader);
                            break;
                        case ACServerProtocol.Checksum:
                            OnChecksum(reader);
                            break;
                        case ACServerProtocol.Chat:
                            OnChat(reader);
                            break;
                        case ACServerProtocol.DamageUpdate:
                            OnDamageUpdate(reader);
                            break;
                        case ACServerProtocol.SectorSplit:
                            OnSectorSplitMessageReceived(reader);
                            break;
                        case ACServerProtocol.LapCompleted:
                            OnLapCompletedMessageReceived(reader);
                            break;
                        case ACServerProtocol.TyreCompoundChange:
                            OnTyreCompoundChange(reader);
                            break;
                        case ACServerProtocol.MandatoryPitUpdate:
                            OnMandatoryPitUpdate(reader);
                            break;
                        case ACServerProtocol.VoteNextSession:
                            OnVoteNextSession(reader);
                            break;
                        case ACServerProtocol.VoteRestartSession:
                            OnVoteRestartSession(reader);
                            break;
                        case ACServerProtocol.VoteKickUser:
                            OnVoteKickUser(reader);
                            break;
                        case ACServerProtocol.ClientEvent:
                            OnClientEvent(reader);
                            break;
                        case ACServerProtocol.Extended:
                            var extendedId = reader.Read<CSPMessageTypeTcp>();
                            Logger.Verbose("Received extended TCP packet with ID {PacketId:X}", id);

                            switch (extendedId)
                            {
                                case CSPMessageTypeTcp.SpectateCar:
                                    OnSpectateCar(reader);
                                    break;
                                case CSPMessageTypeTcp.ClientMessage:
                                    _clientMessageHandler.OnCSPClientMessageTcp(this, reader);
                                    break;
                            }
                            break;
                    }
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
        using var clientEvent = reader.ReadPacket<ClientEvent>();

        foreach (var evt in clientEvent.ClientEvents)
        {
            EntryCar? targetCar = null;

            switch (evt.Type)
            {
                case ClientEventType.CollisionWithCar:
                    targetCar = _entryCarManager.EntryCars[evt.TargetSessionId];
                    Logger.Information("Collision between {SourceCarName} ({SourceCarSessionId}) and {TargetCarName} ({TargetCarSessionId}), rel. speed {Speed:F0}km/h",
                        Name, EntryCar.SessionId, targetCar.Client?.Name ?? targetCar.AiName, targetCar.SessionId, evt.Speed);
                    break;
                case ClientEventType.CollisionWithEnv:
                    Logger.Information("Collision between {SourceCarName} ({SourceCarSessionId}) and environment, rel. speed {Speed:F0}km/h",
                        Name, EntryCar.SessionId, evt.Speed);
                    break;
                case ClientEventType.JumpStartPenalty:
                    Logger.Information("Penalty for {CarName} ({CarSessionId})", Name, EntryCar.SessionId);
                    _entryCarManager.BroadcastPacket(new JumpStartPenalty
                    {
                        SessionId = SessionId
                    });
                    JumpStartPenalty?.Invoke(this, EventArgs.Empty);
                    continue;
            }

            Collision?.Invoke(this, new CollisionEventArgs(targetCar, evt.Speed, evt.Position, evt.RelPosition));
        }
    }

    private void OnSpectateCar(PacketReader reader)
    {
        var spectatePacket = reader.ReadPacket<SpectateCar>();
        if (spectatePacket.SessionId == SessionId || spectatePacket.SessionId > _entryCarManager.EntryCars.Length)
        {
            EntryCar.TargetCar = null;
        }
        else
        {
            EntryCar.TargetCar = _entryCarManager.EntryCars[spectatePacket.SessionId];
        }
    }

    private void OnChecksum(PacketReader reader)
    {
        var allChecksums = _checksumManager.GetChecksumsForHandshake(EntryCar.Model);
        bool passedChecksum = false;
        byte[] fullChecksum = new byte[MD5.HashSizeInBytes * (allChecksums.Count + 1)];
        if (reader.Buffer.Length == fullChecksum.Length + 1)
        {
            reader.ReadBytes(fullChecksum);
            CarChecksum = fullChecksum.AsSpan(fullChecksum.Length - MD5.HashSizeInBytes).ToArray();
            passedChecksum = !_checksumManager.CarChecksums.TryGetValue(EntryCar.Model, out var modelChecksums)
                             || modelChecksums.Count == 0
                             || modelChecksums.Any(c => CarChecksum.AsSpan().SequenceEqual(c.Value));

            for (int i = 0; i < allChecksums.Count; i++)
            {
                if (!allChecksums[i].Value.AsSpan().SequenceEqual(fullChecksum.AsSpan(i * MD5.HashSizeInBytes, MD5.HashSizeInBytes)))
                {
                    Logger.Information("{ClientName} failed checksum for file {ChecksumFile}", Name, allChecksums[i].Key);
                    passedChecksum = false;
                    break;
                }
            }
        }

        ChecksumStatus = passedChecksum ? ChecksumStatus.Succeeded : ChecksumStatus.Failed;

        if (!passedChecksum)
        {
            ChecksumFailed?.Invoke(this, EventArgs.Empty);
            if (HasSentFirstUpdate) KickForFailedChecksum();
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
        EntryCar.SetActive();

        ChatMessage chatMessage = reader.ReadPacket<ChatMessage>();
        chatMessage.SessionId = SessionId;

        if (string.IsNullOrWhiteSpace(chatMessage.Message)) return;

        Logger.Information("CHAT: {ClientName} ({SessionId}): {ChatMessage}", Name, SessionId, chatMessage.Message);

        var args = new ChatMessageEventArgs
        {
            ChatMessage = chatMessage
        };
        ChatMessageReceived?.Invoke(this, args);
    }

    private void OnDamageUpdate(PacketReader reader)
    {
        DamageUpdateIncoming damageUpdate = reader.ReadPacket<DamageUpdateIncoming>();
        EntryCar.Status.DamageZoneLevel = damageUpdate.DamageZoneLevel;

        var update = new DamageUpdate
        {
            SessionId = SessionId,
            DamageZoneLevel = damageUpdate.DamageZoneLevel,
        };
        
        _entryCarManager.BroadcastPacket(update, this);
        Damage?.Invoke(this, new DamageEventArgs(update));
    }

    private void OnTyreCompoundChange(PacketReader reader)
    {
        TyreCompoundChangeRequest compoundChangeRequest = reader.ReadPacket<TyreCompoundChangeRequest>();
        EntryCar.Status.CurrentTyreCompound = compoundChangeRequest.CompoundName;

        var update = new TyreCompoundUpdate
        {
            CompoundName = compoundChangeRequest.CompoundName,
            SessionId = SessionId
        };
        
        _entryCarManager.BroadcastPacket(update);
        TyreCompoundChange?.Invoke(this, new TyreCompoundChangeEventArgs(update));
    }

    private void OnMandatoryPitUpdate(PacketReader reader)
    {
        MandatoryPitRequest mandatoryPitRequest = reader.ReadPacket<MandatoryPitRequest>();
        EntryCar.Status.MandatoryPit = mandatoryPitRequest.MandatoryPit;

        _entryCarManager.BroadcastPacket(new MandatoryPitUpdate
        {
            MandatoryPit = mandatoryPitRequest.MandatoryPit,
            SessionId = SessionId
        });
    }

    private void OnVoteNextSession(PacketReader reader)
    {
        if (!_configuration.Extra.EnableSessionVote) return;
        VoteNextSession voteNextSession = reader.ReadPacket<VoteNextSession>();

        _ = _voteManager.SetVote(SessionId, VoteType.NextSession, voteNextSession.Vote);
    }

    private void OnVoteRestartSession(PacketReader reader)
    {
        if (!_configuration.Extra.EnableSessionVote) return;
        VoteRestartSession voteRestartSession = reader.ReadPacket<VoteRestartSession>();

        _ = _voteManager.SetVote(SessionId, VoteType.RestartSession, voteRestartSession.Vote);
    }

    private void OnVoteKickUser(PacketReader reader)
    {
        if (!_configuration.Extra.EnableKickPlayerVote ||
            _configuration.Extra.VoteKickMinimumConnectedPlayers - 1 > _entryCarManager.ConnectedCars.Count) return;
        VoteKickUser voteKickUser = reader.ReadPacket<VoteKickUser>();

        _ = _voteManager.SetVote(SessionId, VoteType.KickPlayer, voteKickUser.Vote, voteKickUser.TargetSessionId);
    }

    private void OnP2PUpdate(PacketReader reader)
    {
        var push2Pass = reader.ReadPacket<P2PUpdateRequest>();
        if (push2Pass.P2PCount == -1)
        {
            SendPacket(new P2PUpdate
            {
                P2PCount = EntryCar.Status.P2PCount,
                SessionId = SessionId
            });
        }
        else
        {
            if (!_configuration.Extra.EnableUnlimitedP2P && EntryCar.Status.P2PCount > 0)
                EntryCar.Status.P2PCount--;
            
            var update = new P2PUpdate
            {
                Active = push2Pass.Active,
                P2PCount = EntryCar.Status.P2PCount,
                SessionId = SessionId
            };
        
            _entryCarManager.BroadcastPacket(update);
            Push2Pass?.Invoke(this, new Push2PassEventArgs(update));
        }
    }

    private void OnCarListRequest(PacketReader reader)
    {
        CarListRequest carListRequest = reader.ReadPacket<CarListRequest>();

        var carsInPage = _entryCarManager.EntryCars.Skip(carListRequest.PageIndex).Take(10).ToList();
        CarListResponse carListResponse = new CarListResponse
        {
            PageIndex = carListRequest.PageIndex,
            EntryCarsCount = carsInPage.Count,
            EntryCars = carsInPage,
            CarResults = _sessionManager.CurrentSession.Results ?? new Dictionary<byte, EntryCarResult>(),
        };

        CarListResponseSending?.Invoke(this, new CarListResponseSendingEventArgs(carListResponse));
        SendPacket(carListResponse);
    }

    private void OnSectorSplitMessageReceived(PacketReader reader)
    {
        SectorSplitIncoming sectorPacket = reader.ReadPacket<SectorSplitIncoming>();

        // acServer only forwards, no processing
        SectorSplitOutgoing packet = new SectorSplitOutgoing
        {
            SessionId = SessionId,
            SplitIndex = sectorPacket.SplitIndex,
            SplitTime = sectorPacket.SplitTime,
            Cuts = sectorPacket.Cuts
        };
        _entryCarManager.BroadcastPacket(packet);
        SectorSplit?.Invoke(this, new SectorSplitEventArgs(packet));
    }

    private void OnLapCompletedMessageReceived(PacketReader reader)
    {
        LapCompletedIncoming lapPacket = reader.ReadPacket<LapCompletedIncoming>();

        _configuration.Server.DynamicTrack.TotalLapCount++;
        if (_sessionManager.OnLapCompleted(this, lapPacket))
        {
            LapCompletedOutgoing packet = CreateLapCompletedPacket(SessionId, lapPacket.LapTime, lapPacket.Cuts);
            _entryCarManager.BroadcastPacket(packet);
            LapCompleted?.Invoke(this, new LapCompletedEventArgs(packet));
        }
    }

    internal void SendFirstUpdate()
    {
        if (HasSentFirstUpdate)
            return;

        TcpClient.ReceiveTimeout = 0;
        EntryCar.LastPongTime = _sessionManager.ServerTimeMilliseconds;
        HasSentFirstUpdate = true;

        _ = Task.Run(SendFirstUpdateAsync);
    }

    private async Task SendFirstUpdateAsync()
    {
        try
        {
            var connectedCars = _entryCarManager.EntryCars.Where(c => c.Client != null || c.AiControlled).ToList();

            SendPacket(new WelcomeMessage { Message = await _cspServerExtraOptions.GenerateWelcomeMessageAsync(this) });

            _weatherManager.SendWeather(this);

            var batched = new BatchedPacket();
            batched.Packets.Add(new DriverInfoUpdate { ConnectedCars = connectedCars });

            foreach (var car in connectedCars)
            {
                batched.Packets.Add(new MandatoryPitUpdate { MandatoryPit = car.Status.MandatoryPit, SessionId = car.SessionId });
                if (car != EntryCar)
                    batched.Packets.Add(new TyreCompoundUpdate { SessionId = car.SessionId, CompoundName = car.Status.CurrentTyreCompound });

                batched.Packets.Add(new P2PUpdate { SessionId = car.SessionId, P2PCount = car.Status.P2PCount });
                batched.Packets.Add(new BallastUpdate { SessionId = car.SessionId, BallastKg = car.Ballast, Restrictor = car.Restrictor });

                if (_configuration.Extra.AiParams.HideAiCars)
                {
                    batched.Packets.Add(new CSPCarVisibilityUpdate
                    {
                        SessionId = car.SessionId,
                        Visible = car.AiControlled ? CSPCarVisibility.Invisible : CSPCarVisibility.Visible
                    });
                }
            }

            if (EntryCar.FixedSetup != null
                 && _configuration.Setups.Setups.TryGetValue(EntryCar.FixedSetup, out var setup))
                batched.Packets.Add(new CarSetup { Setup = setup.Settings });

            if (_configuration.DrsZones.Zones.Count > 0)
                batched.Packets.Add(new DrsZonesUpdate { Zones = _configuration.DrsZones.Zones });

            if (_configuration.Extra.EnableClientMessages)
            {
                batched.Packets.Add(new CSPHandshakeIn
                {
                    MinVersion = _configuration.CSPTrackOptions.MinimumCSPVersion ?? 0,
                    RequiresWeatherFx = _configuration.Extra.EnableWeatherFx
                });
            }

            SendPacket(batched);

            if (ChecksumStatus == ChecksumStatus.Failed)
            {
                KickForFailedChecksum();
                return;
            }

            if (ChecksumStatus == ChecksumStatus.Pending)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(_configuration.Extra.PlayerChecksumTimeoutSeconds));
                    if (ChecksumStatus != ChecksumStatus.Succeeded && IsConnected)
                    {
                        Log.Information("Checksum request for {ClientName} ({SessionId}) timed out. Consider increasing PlayerChecksumTimeoutSeconds", Name, SessionId);
                        await _entryCarManager.KickAsync(this, KickReason.ChecksumFailed, null, null, $"{Name} did not send the requested checksums.");
                    }
                });
            }

            _entryCarManager.BroadcastPacket(CreateLapCompletedPacket(0xFF, 0, 0));
            FirstUpdateSent?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error sending first update to {ClientName}", Name);
        }
    }

    private void KickForFailedChecksum() => _ = _entryCarManager.KickAsync(this, KickReason.ChecksumFailed, null, null, $"{Name} failed the checksum check and has been kicked.");

    private LapCompletedOutgoing CreateLapCompletedPacket(byte sessionId, uint lapTime, int cuts)
    {
        // TODO: double check and rewrite this
        if (_sessionManager.CurrentSession.Results == null)
            throw new ArgumentNullException(nameof(_sessionManager.CurrentSession.Results));

        var laps = _sessionManager.CurrentSession.Results
            .OrderBy(result => string.IsNullOrEmpty(result.Value.Name))
            .ThenBy(result => result.Value.Name)
            .Select(result => new LapCompletedOutgoing.CompletedLap
            {
                SessionId = result.Key,
                LapTime = _sessionManager.CurrentSession.Configuration.Type == SessionType.Race ? result.Value.TotalTime : result.Value.BestLap,
                NumLaps = (ushort)result.Value.NumLaps,
                HasCompletedLastLap = (byte)(result.Value.HasCompletedLastLap ? 1 : 0),
                RacePos = (byte)result.Value.RacePos,
            })
            .OrderBy(lap => lap.LapTime);

        return new LapCompletedOutgoing
        {
            SessionId = sessionId,
            LapTime = lapTime,
            Cuts = (byte)cuts,
            Laps = laps.ToArray(),
            TrackGrip = _weatherManager.CurrentWeather.TrackGrip
        };
    }

    internal bool TryAssociateUdp(SocketAddress endpoint)
    {
        if (UdpEndpoint != null)
            return false;

        UdpEndpoint = endpoint;
        return true;
    }

    internal async Task DisconnectAsync()
    {
        try
        {
            if (Interlocked.CompareExchange(ref _disconnectRequested, 1, 0) == 1)
                return;

            await Task.Yield();

            if (!string.IsNullOrEmpty(Name))
            {
                Logger.Debug("Disconnecting {ClientName} ({ClientSteamId} - {ClientIpEndpoint})", Name, Guid, (IPEndPoint?)TcpClient.Client.RemoteEndPoint);
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

    public void SendTeleportCarPacket(Vector3 position, Vector3 direction, Vector3 velocity = default)
    {
        SendPacket(new TeleportCarPacket
        {
            Position = position,
            Direction = direction,
            Velocity = velocity,
        });
    }

    internal void LoginAsAdministrator()
    {
        if (IsAdministrator) return;
        
        IsAdministrator = true;
        LoggedInAsAdministrator?.Invoke(this, EventArgs.Empty);
    }

    internal void FireLuaReady()
    {
        LuaReady?.Invoke(this, EventArgs.Empty);
    }

    public void SendChatMessage(string message, byte senderId = 255) => SendPacket(new ChatMessage { Message = message, SessionId = senderId });

    private static string IdFromGuid(ulong guid)
    {
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes($"antarcticfurseal{guid}"));
        return Convert.ToHexStringLower(hash);
    }
    
    private static ulong GuidFromName(string input)
    {
        // https://developer.valvesoftware.com/wiki/SteamID
        // Changing most significant bit so there are no collisions with real Steam IDs
        return XxHash64.HashToUInt64(Encoding.UTF8.GetBytes(input)) | (ulong)1 << 63;
    }
}
