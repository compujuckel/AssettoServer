using AssettoServer.Shared.Network.Packets;
using AssettoServer.Shared.Network.Packets.Outgoing;

namespace AssettoServer.Network.Rcon;

public class AuthResponsePacket : IOutgoingNetworkPacket
{
    public int RequestId { get; set; }
    
    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write(RequestId);
        writer.Write(RconProtocolOut.AuthResponse);
        writer.Write<byte>(0);
    }
}
