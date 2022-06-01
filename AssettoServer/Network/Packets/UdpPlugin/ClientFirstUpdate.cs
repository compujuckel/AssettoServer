using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AssettoServer.Network.Packets.Outgoing;

namespace AssettoServer.Network.Packets.UdpPlugin
{
    public class ClientFirstUpdate : IOutgoingNetworkPacket
    {
        public byte SessionId;

        public void ToWriter(ref PacketWriter writer)
        {
            writer.Write((byte)UdpPluginProtocol.ClientFirstUpdate);
            writer.Write(SessionId);
        }
    }
}
