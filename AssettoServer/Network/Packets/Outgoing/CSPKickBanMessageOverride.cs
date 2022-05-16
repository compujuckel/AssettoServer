using System.Text;

namespace AssettoServer.Network.Packets.Outgoing;

public class CSPKickBanMessageOverride : IOutgoingNetworkPacket
{
    public string? Message;
    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write<byte>(0xAB);
        writer.Write<byte>(0x05);
        writer.WriteString(Message, Encoding.UTF8, 4);
    }
}
