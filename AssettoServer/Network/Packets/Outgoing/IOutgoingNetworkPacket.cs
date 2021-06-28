using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssettoServer.Network.Packets.Outgoing
{
    public interface IOutgoingNetworkPacket
    {
        void ToWriter(ref PacketWriter writer);
    }
}
