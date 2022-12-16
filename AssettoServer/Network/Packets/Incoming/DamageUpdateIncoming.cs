namespace AssettoServer.Network.Packets.Incoming;

public struct DamageUpdateIncoming : IIncomingNetworkPacket
{
    public float[] DamageZoneLevel;

    public void FromReader(ref PacketReader reader)
    {
        DamageZoneLevel = new float[5];
        for (int i = 0; i < 5; i++)
        {
            DamageZoneLevel[i] = reader.Read<float>();
        }
    }
}
