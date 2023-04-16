namespace AssettoServer.Shared.Network.Packets.Outgoing;

public class SunAngleUpdate : IOutgoingNetworkPacket
{
    public float SunAngle;

    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)ACServerProtocol.SunAngleUpdate);
        writer.Write(SunAngle);
    }
}
