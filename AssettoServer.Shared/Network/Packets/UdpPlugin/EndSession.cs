using System.Text;
using AssettoServer.Shared.Network.Packets.Outgoing;

namespace AssettoServer.Shared.Network.Packets.UdpPlugin;

public readonly record struct EndSession : IOutgoingNetworkPacket
{
    public string? ReportJsonFilename { get; init; }

    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)UdpPluginProtocol.EndSession);
        writer.WriteString(ReportJsonFilename, Encoding.UTF8);
    }
}
