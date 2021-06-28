using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssettoServer.Network.Packets.Incoming
{
    public interface IIncomingNetworkPacket
    {
        void FromReader(PacketReader reader);
    }
}
