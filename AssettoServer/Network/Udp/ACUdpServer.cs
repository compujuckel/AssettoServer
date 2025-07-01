using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Shared.Model;
using AssettoServer.Shared.Network.Packets;
using AssettoServer.Shared.Network.Packets.Incoming;
using AssettoServer.Shared.Utils;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace AssettoServer.Network.Udp;

public class ACUdpServer : BackgroundService
{
    private readonly ACServerConfiguration _configuration;
    private readonly SessionManager _sessionManager;
    private readonly EntryCarManager _entryCarManager;
    private readonly CSPClientMessageHandler _clientMessageHandler;
    private readonly ushort _port;
    private readonly Socket _socket;
    
    private readonly ConcurrentDictionary<SocketAddress, EntryCar> _endpointCars = new();
    private static readonly byte[] CarConnectResponse = [(byte)ACServerProtocol.CarConnect];
    private readonly byte[] _lobbyCheckResponse;

    public ACUdpServer(SessionManager sessionManager,
        ACServerConfiguration configuration,
        EntryCarManager entryCarManager,
        CSPClientMessageHandler clientMessageHandler)
    {
        _sessionManager = sessionManager;
        _configuration = configuration;
        _entryCarManager = entryCarManager;
        _clientMessageHandler = clientMessageHandler;
        _port = _configuration.Server.UdpPort;
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        
        _lobbyCheckResponse = new byte[3];
        _lobbyCheckResponse[0] = (byte)ACServerProtocol.LobbyCheck;
        ushort httpPort = _configuration.Server.HttpPort;
        MemoryMarshal.Write(_lobbyCheckResponse.AsSpan(1), in httpPort);
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("Starting UDP server on port {Port}", _port);

        _socket.DisableUdpIcmpExceptions();       
        _socket.ReceiveTimeout = 1000;
        _socket.Bind(new IPEndPoint(IPAddress.Any, _port));
        await Task.Factory.StartNew(() => ReceiveLoop(stoppingToken), TaskCreationOptions.LongRunning);
    }

    private void ReceiveLoop(CancellationToken stoppingToken)
    {
        byte[] buffer = new byte[1500];
        var address = new SocketAddress(AddressFamily.InterNetwork);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var bytesRead = _socket.ReceiveFrom(buffer, SocketFlags.None, address);
                OnReceived(address, buffer, bytesRead);
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut) { }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in UDP receive loop");
            }
        }

        _socket.Dispose();
    }

    public void Send(SocketAddress address, byte[] buffer, int offset, int size)
    {
        _socket.SendTo(buffer.AsSpan(offset, size), SocketFlags.None, address);
    }

    private void OnReceived(SocketAddress address, byte[] buffer, int size)
    {
        try
        {
            var packetReader = new PacketReader(null, buffer.AsMemory()[..size]);

            var packetId = (ACServerProtocol)packetReader.Read<byte>();

            if (packetId == ACServerProtocol.CarConnect)
            {
                int sessionId = packetReader.Read<byte>();
                if (_entryCarManager.ConnectedCars.TryGetValue(sessionId, out EntryCar? car) && car.Client != null)
                {
                    var clonedAddress = address.Clone();
                    if (car.Client.TryAssociateUdp(clonedAddress))
                    {
                        _endpointCars[clonedAddress] = car;
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
            else if (_endpointCars.TryGetValue(address, out var car))
            {
                var client = car.Client;
                if (client == null) return;
                
                if (packetId == ACServerProtocol.SessionRequest)
                {
                    if (_sessionManager.CurrentSession.Configuration.Type != packetReader.Read<SessionType>())
                        _sessionManager.SendCurrentSession(client);
                }
                else if (packetId == ACServerProtocol.PositionUpdate)
                {
                    // Pass checksum first before sending first update + welcome message.
                    // Plugins might rely on checksums to generate CSP extra options
                    if (client.ChecksumStatus != ChecksumStatus.Succeeded) return;
                    
                    if (!client.HasSentFirstUpdate)
                        client.SendFirstUpdate();
                    
                    if (client.SecurityLevel < _configuration.Extra.MandatoryClientSecurityLevel) return;

                    car.UpdatePosition(packetReader.Read<PositionUpdateIn>());
                }
                else if (packetId == ACServerProtocol.PingPong)
                {
                    long currentTime = _sessionManager.ServerTimeMilliseconds;
                    car.Ping = (ushort)(currentTime - packetReader.Read<int>());
                    car.TimeOffset = (int)currentTime - ((car.Ping / 2) + packetReader.Read<int>());
                    car.LastPongTime = currentTime;
                }
                else if (_configuration.Extra.EnableUdpClientMessages && packetId == ACServerProtocol.Extended)
                {
                    var extendedId = packetReader.Read<CSPMessageTypeUdp>();
                    if (extendedId == CSPMessageTypeUdp.ClientMessage)
                    {
                        _clientMessageHandler.OnCSPClientMessageUdp(client, packetReader);
                    }
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
            _endpointCars.TryRemove(sender.UdpEndpoint, out _);
        }
    }
}
