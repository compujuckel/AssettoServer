using AssettoServer.Network.Packets.Shared;

namespace AssettoServer.Network.Packets.Outgoing
{

    public enum CSPCarVisibility
    {
        Visible = 0,
        Invisible = 1
    }
    
    public class CSPCarVisibilityUpdate : IOutgoingNetworkPacket
    {
        public byte SessionId;
        public CSPCarVisibility Visible;
        
        public void ToWriter(ref PacketWriter writer)
        {
            writer.Write((byte)ACServerProtocol.Extended);
            writer.Write((byte)CspMessageType.CarVisibilityUpdate);
            writer.Write<byte>(SessionId);
            writer.Write((byte)Visible);
        }
    }
}