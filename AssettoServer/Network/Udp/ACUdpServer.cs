using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AssettoServer.Network.Packets;
using AssettoServer.Network.Packets.Incoming;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Utils;
using Microsoft.Extensions.Hosting;
using NanoSockets;
using Serilog;

namespace AssettoServer.Network.Udp;

public class ACUdpServer : CriticalBackgroundService
{
    private readonly ACServerConfiguration _configuration;
    private readonly SessionManager _sessionManager;
    private readonly EntryCarManager _entryCarManager;
    private readonly ushort _port;
    private Socket _socket;
    
    private readonly ConcurrentDictionary<Address, EntryCar> _endpointCars = new();
    private static readonly byte[] CarConnectResponse = { (byte)ACServerProtocol.CarConnect };
    private readonly byte[] _lobbyCheckResponse;

    public ACUdpServer(SessionManager sessionManager, ACServerConfiguration configuration, EntryCarManager entryCarManager, IHostApplicationLifetime applicationLifetime) : base(applicationLifetime)
    {
        _sessionManager = sessionManager;
        _configuration = configuration;
        _entryCarManager = entryCarManager;
        _port = _configuration.Server.UdpPort;
        
        _lobbyCheckResponse = new byte[3];
        _lobbyCheckResponse[0] = (byte)ACServerProtocol.LobbyCheck;
        ushort httpPort = _configuration.Server.HttpPort;
        MemoryMarshal.Write(_lobbyCheckResponse.AsSpan()[1..], ref httpPort);
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("Starting UDP server on port {Port}", _port);
        
        UDP.Initialize();
        
        _socket = UDP.Create(256 * 1024, 256 * 1024);
        var address = new Address
        {
            Port = _port
        };

        if (UDP.SetIP(ref address, "0.0.0.0") != Status.OK)
        {
            throw new InvalidOperationException("Could not set UDP address");
        }
        
        if (UDP.Bind(_socket, ref address) != 0)
        {
            throw new InvalidOperationException("Could not bind UDP socket. Maybe the port is already in use?");
        }
        
        await Task.Factory.StartNew(() => ReceiveLoop(stoppingToken), TaskCreationOptions.LongRunning);
    }

    private void ReceiveLoop(CancellationToken stoppingToken)
    {
        byte[] buffer = GC.AllocateArray<byte>(1500, true);
        var address = new Address();
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (UDP.Poll(_socket, 1000) > 0)
                {
                    int dataLength = 0;
                    while ((dataLength = UDP.Receive(_socket, ref address, buffer, buffer.Length)) > 0)
                    {
                        OnReceived(ref address, buffer, dataLength);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in UDP receive loop");
            }
        }
        
        UDP.Destroy(ref _socket);
    }

    public void Send(Address address, byte[] buffer, int offset, int size)
    {
        if (UDP.Send(_socket, ref address, buffer, offset, size) < 0)
        {
            var ip = new StringBuilder(UDP.hostNameSize);
            UDP.GetIP(ref address, ip, ip.Capacity);
            Log.Error("Error sending UDP packet to {Address}", ip.ToString());
        }
    }

    private void OnReceived(ref Address address, byte[] buffer, int size)
    {
        // moved to separate method because it always allocated a closure
        void HighPingKickAsync(EntryCar car)
        {
            _ = Task.Run(() => _entryCarManager.KickAsync(car.Client, $"high ping ({car.Ping}ms)"));
        }
        
        try
        {
            PacketReader packetReader = new PacketReader(null, buffer);

            ACServerProtocol packetId = (ACServerProtocol)packetReader.Read<byte>();

            if (packetId == ACServerProtocol.CarConnect)
            {
                int sessionId = packetReader.Read<byte>();
                if (_entryCarManager.ConnectedCars.TryGetValue(sessionId, out EntryCar? car) && car.Client != null)
                {
                    if (car.Client.TryAssociateUdp(address))
                    {
                        _endpointCars[address] = car;
                        car.Client.Disconnecting += OnClientDisconnecting;
                        
                        Send(address, CarConnectResponse, 0, CarConnectResponse.Length);
                    }
                }
            }
            else if (packetId == ACServerProtocol.LobbyCheck)
            {
                Send(address, _lobbyCheckResponse, 0, _lobbyCheckResponse.Length);
            }
            /*else if (packetId == 0xFF)
            {
                if (buffer.Length > 4
                    && packetReader.Read<byte>() == 0xFF
                    && packetReader.Read<byte>() == 0xFF
                    && packetReader.Read<byte>() == 0xFF)
                {
                    Log.Debug("Steam packet received");

                    byte[] data = buffer.AsSpan().ToArray();
                    Server.Steam.HandleIncomingPacket(data, remoteEp);
                }
            }*/
            else if (_endpointCars.TryGetValue(address, out EntryCar? car) && car.Client != null)
            {
                if (packetId == ACServerProtocol.SessionRequest)
                {
                    if (_sessionManager.CurrentSession.Configuration.Type != packetReader.Read<SessionType>())
                        _sessionManager.SendCurrentSession(car.Client);
                }
                else if (packetId == ACServerProtocol.PositionUpdate)
                {
                    if (!car.Client.HasSentFirstUpdate)
                        car.Client.SendFirstUpdate();

                    car.UpdatePosition(packetReader.Read<PositionUpdateIn>());
                }
                else if (packetId == ACServerProtocol.PingPong)
                {
                    long currentTime = _sessionManager.ServerTimeMilliseconds;
                    car.Ping = (ushort)(currentTime - packetReader.Read<int>());
                    car.TimeOffset = (int)currentTime - ((car.Ping / 2) + packetReader.Read<int>());
                    car.LastPongTime = currentTime;

                    if (car.Ping > _configuration.Extra.MaxPing)
                    {
                        car.HighPingSeconds++;
                        if (car.HighPingSeconds > _configuration.Extra.MaxPingSeconds)
                            HighPingKickAsync(car);
                    }
                    else car.HighPingSeconds = 0;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while receiving a UDP packet");
        }
    }

    private void OnClientDisconnecting(ACTcpClient sender, EventArgs args)
    {
        if (sender.UdpEndpoint != null)
        {
            _endpointCars.TryRemove(sender.UdpEndpoint.Value, out _);
        }
    }
}
