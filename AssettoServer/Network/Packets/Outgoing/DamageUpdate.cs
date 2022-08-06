namespace AssettoServer.Network.Packets.Outgoing;

public class DamageUpdate : IOutgoingNetworkPacket
{
    public byte SessionId;
    public float[] DamageZoneLevel = null!;

    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)ACServerProtocol.DamageUpdate);
        writer.Write(SessionId);

        for (int i = 0; i < 5; i++)
            writer.Write(DamageZoneLevel[i]);
    }
}
