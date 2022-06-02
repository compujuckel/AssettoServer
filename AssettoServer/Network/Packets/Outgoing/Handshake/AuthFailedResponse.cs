using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssettoServer.Network.Packets.Outgoing.Handshake
{
    public readonly struct AuthFailedResponse : IOutgoingNetworkPacket
    {
        public readonly string Reason;

        public AuthFailedResponse(string reason)
        {
            Reason = reason;
        }

        public void ToWriter(ref PacketWriter writer)
        {
            writer.Write((byte)ACServerProtocol.AuthFailed);
            writer.WriteUTF32String(Reason);
        }
    }
}
