using AssettoServer.Shared.Model;

namespace AssettoServer.Shared.Network.Packets.Incoming;

public struct DamageUpdateIncoming : IIncomingNetworkPacket
{
    public DamageZoneLevel DamageZoneLevel;

    public void FromReader(PacketReader reader)
    {
        DamageZoneLevel = reader.Read<DamageZoneLevel>();
    }
}
