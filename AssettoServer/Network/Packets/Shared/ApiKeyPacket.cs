using System.Text;
using AssettoServer.Network.Packets.Outgoing;

namespace AssettoServer.Network.Packets.Shared;

public class ApiKeyPacket : IOutgoingNetworkPacket
{
    public const int Id = 0x27612FAB;
    public required string Key { get; init; }

    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)ACServerProtocol.Extended);
        writer.Write((byte)CSPMessageTypeTcp.ClientMessage);
        writer.Write<byte>(255);
        writer.Write<ushort>(60000);
        writer.Write(Id);
        writer.WriteStringFixed(Key, Encoding.ASCII, 32, false);
    }
}
