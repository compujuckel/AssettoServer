using AssettoServer.Shared.Network.Packets.Shared;

namespace AssettoServer.Shared.Network.Packets.Outgoing;

public class CSPHandshakeIn : IOutgoingNetworkPacket
{
    public uint MinVersion { get; init; }
    public bool RequiresWeatherFx { get; init; }
    
    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)ACServerProtocol.Extended);
        writer.Write((byte)CSPMessageTypeTcp.ClientMessage);
        writer.Write((byte)255);
        writer.Write((ushort)CSPClientMessageType.HandshakeIn);
        writer.Write(MinVersion);
        writer.Write(RequiresWeatherFx);
    }
}
