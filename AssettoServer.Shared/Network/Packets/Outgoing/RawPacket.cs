namespace AssettoServer.Shared.Network.Packets.Outgoing;

public class RawPacket : IOutgoingNetworkPacket
{
    public byte[]? Content;
        
    public void ToWriter(ref PacketWriter writer)
    {
        if (Content == null)
            throw new ArgumentNullException(nameof(Content));
            
        writer.WriteBytes(Content);
    }
}
