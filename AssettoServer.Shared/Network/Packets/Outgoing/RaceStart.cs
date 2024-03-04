namespace AssettoServer.Shared.Network.Packets.Outgoing;

public class RaceStart : IOutgoingNetworkPacket
{
    public int TimeOffset;
    public uint StartTime;
    public ushort Ping;
    
    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)ACServerProtocol.RaceStart);
        writer.Write(TimeOffset);
        writer.Write(StartTime);
        writer.Write(Ping);
    }
}
