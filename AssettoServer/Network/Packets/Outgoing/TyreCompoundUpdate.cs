using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssettoServer.Network.Packets.Outgoing
{
    public class TyreCompoundUpdate : IOutgoingNetworkPacket
    {
        public string? CompoundName;
        public byte SessionId;

        public void ToWriter(ref PacketWriter writer)
        {
            writer.Write((byte)ACServerProtocol.TyreCompoundChange);
            writer.Write(SessionId);
            writer.WriteASCIIString(CompoundName);
        }
    }
}
