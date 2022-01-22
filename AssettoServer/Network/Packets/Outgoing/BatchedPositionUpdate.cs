using AssettoServer.Network.Packets.Shared;

namespace AssettoServer.Network.Packets.Outgoing;

public class BatchedPositionUpdate : IOutgoingNetworkPacket
{
    public PositionUpdate[] Updates;
    
    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write<byte>(0x48);
        writer.Write<int>(0); // ignored
        writer.Write<short>(0); // ignored
        writer.Write((byte)Updates.Length);
        for (int i = 0; i < Updates.Length; i++)
        {
            Updates[i].IsBatched = true;
            Updates[i].ToWriter(ref writer);
        }
    }
}