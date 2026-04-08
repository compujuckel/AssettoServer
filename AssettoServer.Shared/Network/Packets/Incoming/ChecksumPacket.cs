using AssettoServer.Shared.Network.Packets.Outgoing;

namespace AssettoServer.Shared.Network.Packets.Incoming;

public class ChecksumPacket : IIncomingNetworkPacket, IOutgoingNetworkPacket
{
    public byte[] Checksum = null!;
    
    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)ACServerProtocol.Checksum);
        writer.WriteBytes(Checksum);
    }
    
    public void FromReader(PacketReader reader)
    {
        Checksum = new byte[reader.Buffer.Length - 1];
        reader.ReadBytes(Checksum);
    }
}
