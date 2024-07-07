namespace AssettoServer.Shared.Network.Packets.Outgoing;

public class JumpStartPenalty : IOutgoingNetworkPacket
{
    public byte SessionId;

    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)ACServerProtocol.ClientEvent);
        writer.Write((byte)ClientEventType.JumpStartPenalty);
        writer.Write(SessionId);
    }
}
