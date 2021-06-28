using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssettoServer.Network.Packets.Outgoing
{
    public class MandatoryPitUpdate : IOutgoingNetworkPacket
    {
        public byte SessionId;
        public bool MandatoryPit;

        public void ToWriter(ref PacketWriter writer)
        {
            writer.Write<byte>(0xE);
            writer.Write(SessionId);
            writer.Write(MandatoryPit);
        }
    }
}
