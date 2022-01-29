using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssettoServer.Network.Packets.Outgoing
{
    public class CarConnected : IOutgoingNetworkPacket
    {
        public byte SessionId;
        public string? Name;
        public string? Nation;

        public void ToWriter(ref PacketWriter writer)
        {
            writer.Write<byte>(0x5A);
            writer.Write(SessionId);
            writer.WriteASCIIString(Name);
            writer.WriteASCIIString(Nation);
        }
    }
}
