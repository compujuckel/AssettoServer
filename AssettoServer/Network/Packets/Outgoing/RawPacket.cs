namespace AssettoServer.Network.Packets.Outgoing
{
    public class RawPacket : IOutgoingNetworkPacket
    {
        public byte[] Content;
        
        public void ToWriter(ref PacketWriter writer)
        {
            writer.WriteBytes(Content);
        }
    }
}