using System.Text;

namespace AssettoServer.Shared.Network.Packets.Outgoing;

public class CSPKickBanMessageOverride : IOutgoingNetworkPacket
{
    public string? Message;
    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)ACServerProtocol.Extended);
        writer.Write((byte)CSPMessageTypeTcp.KickBanMessage);
        writer.WriteString(Message, Encoding.UTF8, 4);
    }
}
