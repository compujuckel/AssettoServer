using AssettoServer.Shared.Network.Packets.Incoming;

namespace AssettoServer.Shared.Network.Packets.Outgoing;

public struct PingRequest : IOutgoingNetworkPacket, IIncomingNetworkPacket
{
    public uint Time;
    public ushort CurrentPing;

    public PingRequest(uint time, ushort currentPing)
    {
        Time = time;
        CurrentPing = currentPing;
    }

    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)ACServerProtocol.PingUpdate);
        writer.Write(Time);
        writer.Write(CurrentPing);
    }

    public void FromReader(PacketReader reader)
    {
        Time = reader.Read<uint>();
        CurrentPing = reader.Read<ushort>();
    }
}
