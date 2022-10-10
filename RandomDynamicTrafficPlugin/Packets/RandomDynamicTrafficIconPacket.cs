
using AssettoServer.Network.Packets;
using AssettoServer.Network.Packets.Outgoing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RandomDynamicTrafficPlugin.Packets
{
    public class RandomDynamicTrafficIconPacket : IOutgoingNetworkPacket
    {
        public TrafficState TrafficState { get; set; }
        public void ToWriter(ref PacketWriter writer)
        {
            writer.Write((byte)ACServerProtocol.Extended);
            writer.Write((byte)CSPMessageTypeTcp.ClientMessage);
            writer.Write<byte>(255);
            writer.Write<ushort>(60000);
            writer.Write(0xD8F297C1);            
            writer.Write<byte>((byte)TrafficState);            
        }
    }
}

public enum TrafficState : byte
{
    LOW = 1,
    CASUAL = 2,
    PEAK = 3,
    ACCIDENT = 4
}
