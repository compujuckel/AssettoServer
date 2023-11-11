using AssettoServer.Shared.Model;

namespace AssettoServer.Shared.Network.Packets.Outgoing;

public class DamageUpdate : IOutgoingNetworkPacket
{
    public byte SessionId;
    public DamageZoneLevel DamageZoneLevel;

    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write(ACServerProtocol.DamageUpdate);
        writer.Write(SessionId);
        writer.Write(DamageZoneLevel);
    }
}
