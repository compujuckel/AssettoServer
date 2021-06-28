using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssettoServer.Network.Packets.Outgoing
{
    public class P2PUpdate : IOutgoingNetworkPacket
    {
        public byte SessionId;
        public short P2PCount;
        public bool Active;

        public void ToWriter(ref PacketWriter writer)
        {
            writer.Write<byte>(0xD);
            writer.Write(SessionId);
            writer.Write(P2PCount);
            writer.Write(Active);
        }
    }
}
