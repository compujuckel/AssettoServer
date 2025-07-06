using AssettoServer.Shared.Network.Packets.Outgoing;

namespace AssettoServer.Shared.Network.Packets.Incoming;

public class PingResponse : IIncomingNetworkPacket, IOutgoingNetworkPacket
{
    public int Time;
    public int ClientTime;
    
    public void FromReader(PacketReader reader)
    {
        Time = reader.Read<int>();
        ClientTime = reader.Read<int>();
    }

    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write(ACServerProtocol.PingPong);
        writer.Write(Time);
        writer.Write(ClientTime);
    }
}
