using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssettoServer.Network.Tcp;
using AssettoServer.Server.Admin;
using AssettoServer.Server.Blacklist;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.OpenSlotFilters;
using AssettoServer.Shared.Network.Packets.Incoming;
using AssettoServer.Shared.Network.Packets.Outgoing;
using AssettoServer.Shared.Network.Packets.Shared;
using Serilog;

namespace AssettoServer.Server;

public class EntryCarManager
{
    public EntryCar[] EntryCars { get; private set; } = Array.Empty<EntryCar>();
    internal ConcurrentDictionary<int, EntryCar> ConnectedCars { get; } = new();

    private readonly ACServerConfiguration _configuration;
    private readonly IBlacklistService _blacklist;
    private readonly EntryCar.Factory _entryCarFactory;
    private readonly IAdminService _adminService;
    private readonly SemaphoreSlim _connectSemaphore = new(1, 1);
    private readonly Lazy<OpenSlotFilterChain> _openSlotFilterChain;

    /// <summary>
    /// Fires when a client has secured a slot and established a TCP connection.
    /// </summary>
    public event EventHandler<ACTcpClient, EventArgs>? ClientConnected;
    
    /// <summary>
    /// Fires when a client has been kicked.
    /// </summary>
    public event EventHandler<ACTcpClient, ClientAuditEventArgs>? ClientKicked;
        
    /// <summary>
    /// Fires when a client has been banned.
    /// </summary>
    public event EventHandler<ACTcpClient, ClientAuditEventArgs>? ClientBanned;
    
    /// <summary>
    /// Fires when a player has disconnected.
    /// </summary>
    public event EventHandler<ACTcpClient, EventArgs>? ClientDisconnected;

    public EntryCarManager(ACServerConfiguration configuration, EntryCar.Factory entryCarFactory, IBlacklistService blacklist, IAdminService adminService, Lazy<OpenSlotFilterChain> openSlotFilterChain)
    {
        _configuration = configuration;
        _entryCarFactory = entryCarFactory;
        _blacklist = blacklist;
        _adminService = adminService;
        _openSlotFilterChain = openSlotFilterChain;
    }

    public async Task KickAsync(ACTcpClient? client, string? reason = null, ACTcpClient? admin = null)
    {
        if (client == null) return;
        
        string? clientReason = reason != null ? $"You have been kicked for {reason}" : null;
        string broadcastReason = reason != null ? $"{client.Name} has been kicked from the server for {reason}." : $"{client.Name} has been kicked from the server.";

        await KickAsync(client, KickReason.Kicked, reason, clientReason, broadcastReason, admin);
    }
    
    public async Task BanAsync(ACTcpClient? client, string? reason = null, ACTcpClient? admin = null)
    {
        if (client == null) return;
        
        string clientReason = reason != null ? $"You have been banned for {reason}" : "You have been banned from the server";
        string broadcastReason = reason != null ? $"{client.Name} has been banned from the server for {reason}." : $"{client.Name} has been banned from the server.";

        await KickAsync(client, KickReason.VoteBlacklisted, reason, clientReason, broadcastReason, admin);
        await _blacklist.AddAsync(client.Guid);
        if (client.OwnerGuid.HasValue && client.Guid != client.OwnerGuid)
        {
            await _blacklist.AddAsync(client.OwnerGuid.Value);
        }
    }

    public async Task KickAsync(ACTcpClient? client, KickReason reason, string? auditReason = null, string? clientReason = null, string? broadcastReason = null, ACTcpClient? admin = null)
    {
        if (client != null && !client.IsDisconnectRequested)
        {
            if (broadcastReason != null)
            {
                BroadcastPacket(new ChatMessage { SessionId = 255, Message = broadcastReason });
            }

            if (clientReason != null)
            {
                client.SendPacket(new CSPKickBanMessageOverride { Message = clientReason });
            }
            
            client.SendPacket(new KickCar { SessionId = client.SessionId, Reason = reason });
            
            var args = new ClientAuditEventArgs
            {
                Reason = reason,
                ReasonStr = broadcastReason,
                Admin = admin
            };
            if (reason is KickReason.Kicked or KickReason.VoteKicked)
            {
                client.Logger.Information("{ClientName} was kicked. Reason: {Reason}", client.Name, auditReason ?? "No reason given.");
                ClientKicked?.Invoke(client, args);
            }
            else if (reason is KickReason.VoteBanned or KickReason.VoteBlacklisted)
            {
                client.Logger.Information("{ClientName} was banned. Reason: {Reason}", client.Name, auditReason ?? "No reason given.");
                ClientBanned?.Invoke(client, args);
            }

            await client.DisconnectAsync();
        }
    }

    internal async Task DisconnectClientAsync(ACTcpClient client)
    {
        try
        {
            await _connectSemaphore.WaitAsync();
            if (client.IsConnected && client.EntryCar.Client == client && ConnectedCars.TryRemove(client.SessionId, out _))
            {
                client.Logger.Information("{ClientName} has disconnected", client.Name);
                client.EntryCar.Client = null;
                client.IsConnected = false;

                if (client.ChecksumStatus == ChecksumStatus.Succeeded)
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
            _connectSemaphore.Release();
        }
    }

    public void BroadcastPacket<TPacket>(TPacket packet, ACTcpClient? sender = null) where TPacket : IOutgoingNetworkPacket
    {
        foreach (var car in EntryCars)
        {
            if (car.Client is { HasSentFirstUpdate: true } && car.Client != sender)
            {
                car.Client?.SendPacket(packet);
            }
        }
    }
        
    public void BroadcastPacketUdp<TPacket>(in TPacket packet, ACTcpClient? sender = null, float? range = null, bool skipSender = true) where TPacket : IOutgoingNetworkPacket
    {
        foreach (var car in EntryCars)
        {
            if (car.Client is { HasSentFirstUpdate: true, UdpEndpoint: not null } 
                && (!skipSender || car.Client != sender)
                && (!range.HasValue || (sender != null && sender.EntryCar.IsInRange(car, range.Value))))
            {
                car.Client?.SendPacketUdp(in packet);
            }
        }
    }

    internal async Task<bool> TrySecureSlotAsync(ACTcpClient client, HandshakeRequest handshakeRequest)
    {
        try
        {
            await _connectSemaphore.WaitAsync();

            if (ConnectedCars.Count >= _configuration.Server.MaxClients)
                return false;
            
            for (int i = 0; i < EntryCars.Length; i++)
            {
                EntryCar entryCar = EntryCars[i];

                bool isAdmin = await _adminService.IsAdminAsync(handshakeRequest.Guid);

                if (entryCar.Client == null
                    && handshakeRequest.RequestedCar == entryCar.Model
                    && (isAdmin || _openSlotFilterChain.Value.IsSlotOpen(entryCar, handshakeRequest.Guid)))
                {
                    entryCar.Reset();
                    entryCar.Client = client;
                    client.EntryCar = entryCar;
                    client.SessionId = entryCar.SessionId;
                    client.IsConnected = true;
                    client.IsAdministrator = isAdmin;
                    client.Guid = handshakeRequest.Guid;

                    ConnectedCars[client.SessionId] = entryCar;

                    ClientConnected?.Invoke(client, EventArgs.Empty);
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
            _connectSemaphore.Release();
        }

        return false;
    }

    internal void Initialize()
    {
        EntryCars = new EntryCar[Math.Min(_configuration.Server.MaxClients, _configuration.EntryList.Cars.Count)];
        Log.Information("Loaded {Count} cars", EntryCars.Length);
        for (int i = 0; i < EntryCars.Length; i++)
        {
            var entry = _configuration.EntryList.Cars[i];
            var driverOptions = CSPDriverOptions.Parse(entry.Skin);
            var aiMode = _configuration.Extra.EnableAi ? entry.AiMode : AiMode.None;

            EntryCars[i] = _entryCarFactory(entry.Model, entry.Skin, (byte)i);
            EntryCars[i].SpectatorMode = entry.SpectatorMode;
            EntryCars[i].Ballast = entry.Ballast;
            EntryCars[i].Restrictor = entry.Restrictor;
            EntryCars[i].FixedSetup = entry.FixedSetup;
            EntryCars[i].DriverOptionsFlags = driverOptions;
            EntryCars[i].AiMode = aiMode;
            EntryCars[i].AiEnableColorChanges = driverOptions.HasFlag(DriverOptionsFlags.AllowColorChange);
            EntryCars[i].AiControlled = aiMode != AiMode.None;
            EntryCars[i].NetworkDistanceSquared = MathF.Pow(_configuration.Extra.NetworkBubbleDistance, 2);
            EntryCars[i].OutsideNetworkBubbleUpdateRateMs = 1000 / _configuration.Extra.OutsideNetworkBubbleRefreshRateHz;
            if (!string.IsNullOrWhiteSpace(entry.Guid))
            {
                EntryCars[i].AllowedGuids = entry.Guid.Split(';').Select(ulong.Parse).ToList();
            }
        }
    }
}
