using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AssettoServer.Network.Packets.Outgoing;

namespace AssettoServer.Network.Packets.UdpPlugin
{
    // TODO: this is currently unused
    public readonly record struct EndSession : IOutgoingNetworkPacket
    {
        public string? ReportJsonFilename { get; init; }

        public void ToWriter(ref PacketWriter writer)
        {
            writer.Write((byte)UdpPluginProtocol.EndSession);
            writer.WriteString(ReportJsonFilename, Encoding.UTF8);
        }
    }
}
