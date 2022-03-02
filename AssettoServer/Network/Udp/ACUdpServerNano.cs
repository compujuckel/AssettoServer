using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using AssettoServer.Network.Packets;
using AssettoServer.Network.Packets.Incoming;
using AssettoServer.Network.Packets.Outgoing;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using NanoSockets;
using Serilog;

namespace AssettoServer.Network.Udp;

public class ACUdpServerNano
{
    private readonly ACServer _server;
    private readonly ushort _port;
    private Socket _socket;

    public ACUdpServerNano(ACServer server, ushort port)
    {
        _server = server;
        _port = port;
    }
    
    public void Start()
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
            throw new InvalidOperationException($"Could not bind UDP socket. Maybe the port is already in use?");
        }
        
        _ = Task.Factory.StartNew(ReceiveLoop, TaskCreationOptions.LongRunning);
    }

    private void ReceiveLoop()
    {
        byte[] buffer = new byte[1500];
        var address = new Address();
        
        while (true)
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
        try
        {
            PacketReader packetReader = new PacketReader(null, buffer);
            int packetId = packetReader.Read<byte>();
            if (packetId == 0x4E)
            {
                int sessionId = packetReader.Read<byte>();
                if (_server.ConnectedCars.TryGetValue(sessionId, out EntryCar? car) && car.Client != null)
                {
                    if (car.Client.TryAssociateUdp(address))
                    {
                        _server.EndpointCars[address] = car;

                        byte[] response = new byte[1] { 0x4E };
                        Send(address, response, 0, 1);
                    }
                }
            }
            else if (packetId == 0xC8)
            {
                ushort httpPort = (ushort)_server.Configuration.Server.HttpPort;
                MemoryMarshal.Write(buffer.AsSpan().Slice(1), ref httpPort);
                Send(address, buffer, 0, 3);
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
            else if (_server.EndpointCars.TryGetValue(address, out EntryCar? car) && car.Client != null)
            {
                if (packetId == 0x4F)
                {
                    if (_server.CurrentSession.Configuration.Type != packetReader.Read<SessionType>())
                        _server.SendCurrentSession(car.Client);
                }
                else if (packetId == 0x46)
                {
                    if (!car.Client.HasSentFirstUpdate)
                        car.Client.SendFirstUpdate();

                    car.UpdatePosition(packetReader.Read<PositionUpdateIn>());
                }
                else if (packetId == 0xF8)
                {
                    int currentTime = _server.CurrentTime;
                    car.Ping = (ushort)(currentTime - packetReader.Read<int>());
                    car.TimeOffset = currentTime - ((car.Ping / 2) + packetReader.Read<int>());
                    car.LastPongTime = currentTime;

                    if (car.Ping > _server.Configuration.Extra.MaxPing)
                    {
                        car.HighPingSeconds++;
                        if (car.HighPingSeconds > _server.Configuration.Extra.MaxPingSeconds)
                            _ = _server.KickAsync(car.Client, KickReason.Kicked, $"{car.Client?.Name} has been kicked for high ping ({car.Ping}ms).");
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
}