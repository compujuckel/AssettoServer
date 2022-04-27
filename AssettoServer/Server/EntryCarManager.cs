using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using AssettoServer.Network.Packets.Incoming;
using AssettoServer.Network.Packets.Outgoing;
using AssettoServer.Network.Packets.Shared;
using AssettoServer.Network.Tcp;
using AssettoServer.Server.Admin;
using AssettoServer.Server.Blacklist;
using AssettoServer.Server.Configuration;
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

    public EntryCarManager(ACServerConfiguration configuration, EntryCar.Factory entryCarFactory, IBlacklistService blacklist, IAdminService adminService)
    {
        _configuration = configuration;
        _entryCarFactory = entryCarFactory;
        _blacklist = blacklist;
        _adminService = adminService;
    }

    /// <summary>
    /// Kick a client from the server.
    /// </summary>
    /// <param name="client">The client.</param>
    /// <param name="reason">Kick reason that will be shown to the kicked player in the red screen.</param>
    /// <param name="reasonStr">Reason for the kick, will be shown in the broadcasted message and in logs.</param>
    /// <param name="broadcastMessage">When true, announce the kick to all connected players.</param>
    /// <param name="admin">The admin that kicked the player.</param>
    public async Task KickAsync(ACTcpClient? client, KickReason reason, string? reasonStr = null, bool broadcastMessage = true, ACTcpClient? admin = null)
    {
        if (client != null && !client.IsDisconnectRequested)
        {
            if (reasonStr != null && broadcastMessage)
                BroadcastPacket(new ChatMessage { SessionId = 255, Message = reasonStr });

            client.Logger.Information("{ClientName} was kicked. Reason: {Reason}", client.Name, reasonStr ?? "No reason given.");
            client.SendPacket(new KickCar { SessionId = client.SessionId, Reason = reason });

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
                BroadcastPacket(new ChatMessage { SessionId = 255, Message = reasonStr });

            client.Logger.Information("{ClientName} was banned. Reason: {Reason}", client.Name, reasonStr ?? "No reason given.");
            client.SendPacket(new KickCar { SessionId = client.SessionId, Reason = reason });

            var args = new ClientAuditEventArgs
            {
                Reason = reason,
                ReasonStr = reasonStr,
                Admin = admin
            };
            ClientBanned?.Invoke(client, args);

            await client.DisconnectAsync();
            await _blacklist.AddAsync(ulong.Parse(client.Guid));
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
            _connectSemaphore.Release();
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
                if (entryCar.Client != null && entryCar.Client.Guid == client.Guid)
                    return false;

                var isAdmin = !string.IsNullOrEmpty(handshakeRequest.Guid) && await _adminService.IsAdminAsync(ulong.Parse(handshakeRequest.Guid));
                    
                if (entryCar.AiMode != AiMode.Fixed 
                    && (isAdmin || _configuration.Extra.AiParams.MaxPlayerCount == 0 || ConnectedCars.Count < _configuration.Extra.AiParams.MaxPlayerCount) 
                    && entryCar.Client == null && handshakeRequest.RequestedCar == entryCar.Model)
                {
                    entryCar.Reset();
                    entryCar.Client = client;
                    client.EntryCar = entryCar;
                    client.SessionId = entryCar.SessionId;
                    client.IsConnected = true;
                    client.IsAdministrator = isAdmin;

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
            EntryCars[i].DriverOptionsFlags = driverOptions;
            EntryCars[i].AiMode = aiMode;
            EntryCars[i].AiEnableColorChanges = driverOptions.HasFlag(DriverOptionsFlags.AllowColorChange);
            EntryCars[i].AiControlled = aiMode != AiMode.None;
            EntryCars[i].NetworkDistanceSquared = MathF.Pow(_configuration.Extra.NetworkBubbleDistance, 2);
            EntryCars[i].OutsideNetworkBubbleUpdateRateMs = 1000 / _configuration.Extra.OutsideNetworkBubbleRefreshRateHz;
        }
    }
}
