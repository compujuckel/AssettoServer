namespace AssettoServer.Shared.Network.Packets.Outgoing;

public class RaceStart : IOutgoingNetworkPacket
{
    public int StartTime;
    public uint TimeOffset;
    public ushort Ping;
    
    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)ACServerProtocol.RaceStart);
        writer.Write(StartTime);
        writer.Write(TimeOffset);
        writer.Write(Ping);
    }
}
