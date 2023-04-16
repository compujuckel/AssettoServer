namespace AssettoServer.Shared.Network.Packets.Outgoing;

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
        writer.Write((byte)CSPMessageTypeTcp.CarVisibilityUpdate);
        writer.Write<byte>(SessionId);
        writer.Write((byte)Visible);
    }
}
