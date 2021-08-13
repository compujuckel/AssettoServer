using AssettoServer.Network.Packets;
using AssettoServer.Network.Packets.Outgoing;
using AssettoServer.Network.Packets.Shared;
using AssettoServer.Server;
using NetCoreServer;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AssettoServer.Network.Udp
{
    public class ACUdpServer : UdpServer
    {
        public ACServer Server { get; }

        public long BytesSentPerSecond { get; private set; }
        public long BytesReceivedPerSecond { get; private set; }

        public long DatagramsSentPerSecond { get; private set; }
        public long DatagramsReceivedPerSecond { get; private set; }

        private long LastBytesSent { get; set; }
        private long LastBytesReceived { get; set; }
        private long LastDatagramsSent { get; set; }
        private long LastDatagramsReceived { get; set; }

        private long LastStatsUpdateTime { get; set; }

        public ACUdpServer(ACServer server, int port) : base(IPAddress.Any, port)
        {
            Server = server;
        }

        protected override void OnStarted()
        {
            Log.Information("Starting UDP server on port {0}.", Server.Configuration.UdpPort);
            ReceiveAsync();
        }

        protected override void OnReceived(EndPoint endpoint, byte[] buffer, long offset, long size)
        {

            try
            {
                IPEndPoint remoteEp = (IPEndPoint)endpoint;
                PacketReader packetReader = new PacketReader(null, buffer);
                int packetId = packetReader.Read<byte>();
                if (packetId == 0x4E)
                {
                    int sessionId = packetReader.Read<byte>();
                    if (Server.ConnectedCars.TryGetValue(sessionId, out EntryCar car) && car.Client != null)
                    {
                        if (car.Client.TryAssociateUdp(remoteEp))
                        {
                            Server.EndpointCars[remoteEp] = car;

                            byte[] response = new byte[1] { 0x4E };
                            SendAsync(endpoint, response, 0, 1);
                        }
                    }
                }
                else if (packetId == 0xC8)
                {
                    ushort httpPort = (ushort)Server.Configuration.HttpPort;
                    MemoryMarshal.Write(buffer.AsSpan().Slice(1), ref httpPort);
                    SendAsync(endpoint, buffer, 0, 3);
                }
                else if (Server.EndpointCars.TryGetValue(remoteEp, out EntryCar car) && car.Client != null)
                {
                    if (packetId == 0x4F)
                    {
                        if (Server.CurrentSession.Type != packetReader.Read<byte>())
                            car.Client.SendCurrentSession();
                    }
                    else if (packetId == 0x46)
                    {
                        if (!car.Client.HasSentFirstUpdate)
                            car.Client.SendFirstUpdate();

                        car.UpdatePosition(packetReader.ReadPacket<PositionUpdate>());
                    }
                    else if (packetId == 0xF8)
                    {
                        int currentTime = Server.CurrentTime;
                        car.Ping = (ushort)(currentTime - packetReader.Read<int>());
                        car.TimeOffset = currentTime - ((car.Ping / 2) + packetReader.Read<int>());
                        car.LastPongTime = currentTime;

                        if (car.Ping > Server.Configuration.Extra.MaxPing)
                        {
                            car.HighPingSeconds++;
                            if (car.HighPingSeconds > Server.Configuration.Extra.MaxPingSeconds)
                                _ = Server.KickAsync(car?.Client, KickReason.None, $"{car.Client?.Name} has been kicked for high ping ({car.Ping}ms).");
                        }
                        else car.HighPingSeconds = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while receiving a UDP packet.");
            }
            finally
            {
                ThreadPool.QueueUserWorkItem(o => { ReceiveAsync(); });
            }

        }

        protected override void OnError(SocketError error)
        {
            Log.Error("UDP Server caught an error with code {0}", error);
        }

        internal void UpdateStatistics()
        {
            if(Environment.TickCount64 - LastStatsUpdateTime > 1000)
            {
                LastStatsUpdateTime = Environment.TickCount64;

                BytesSentPerSecond = BytesSent - LastBytesSent;
                BytesReceivedPerSecond = BytesReceived - LastBytesReceived;
                DatagramsSentPerSecond = DatagramsSent - LastDatagramsSent;
                DatagramsReceivedPerSecond = DatagramsReceived - LastDatagramsReceived;

                LastBytesSent = BytesSent;
                LastBytesReceived = BytesReceived;
                LastDatagramsSent = DatagramsSent;
                LastDatagramsReceived = DatagramsReceived;
            }
        }
    }
}
