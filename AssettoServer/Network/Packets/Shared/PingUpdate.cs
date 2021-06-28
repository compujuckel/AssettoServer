using AssettoServer.Network.Packets.Outgoing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssettoServer.Network.Packets.Shared
{
    public struct PingUpdate : IOutgoingNetworkPacket
    {
        public int Time;
        public ushort CurrentPing;

        public void ToWriter(ref PacketWriter writer)
        {
            writer.Write<byte>(0xF9);
            writer.Write(Time);
            writer.Write(CurrentPing);
        }
    }
}
