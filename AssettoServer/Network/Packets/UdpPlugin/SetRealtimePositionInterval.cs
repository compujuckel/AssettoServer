using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AssettoServer.Network.Packets.Incoming;

namespace AssettoServer.Network.Packets.UdpPlugin
{
    public struct SetRealtimePositionInterval : IIncomingNetworkPacket
    {
        public ushort Interval;

        public void FromReader(PacketReader reader)
        {
            Interval = reader.Read<ushort>();
        }
    }
}
