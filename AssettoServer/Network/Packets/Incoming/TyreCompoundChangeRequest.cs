using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssettoServer.Network.Packets.Incoming
{
    public struct TyreCompoundChangeRequest : IIncomingNetworkPacket
    {
        public string CompoundName;

        public void FromReader(PacketReader reader)
        {
            CompoundName = reader.ReadASCIIString();
        }
    }
}
