namespace AssettoServer.Network.Packets.Outgoing
{
    public class CSPCarVisibilityUpdate : IOutgoingNetworkPacket
    {
        public byte SessionId;
        public bool Visible;
        
        public void ToWriter(ref PacketWriter writer)
        {
            writer.Write<byte>(0xAB);
            writer.Write<byte>(0x2);
            writer.Write<byte>(SessionId);
            writer.Write((byte)(Visible ? 0 : 1));
        }
    }
}