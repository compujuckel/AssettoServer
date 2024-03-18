using AssettoServer.Shared.Model;

namespace AssettoServer.Shared.Network.Packets.Outgoing;

public class DrsZonesUpdate : IOutgoingNetworkPacket
{
    public required IEnumerable<IDrsZone> Zones;

    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)ACServerProtocol.DrsZonesUpdate);
        writer.Write((byte)Zones.Count());
        foreach(var zone in Zones)
        {
            writer.Write(zone.Start);
            writer.Write(zone.End);
        }
    }
}
