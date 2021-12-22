using AssettoServer.Network.Packets.Incoming;
using AssettoServer.Network.Packets.Outgoing;

namespace AssettoServer.Network.Packets.Shared;

public struct CSPClientMessage : IOutgoingNetworkPacket, IIncomingNetworkPacket
{
    public byte SessionId;
    public short Type;
    public byte[] Data;


    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write(0xAB);
        writer.Write(0x03);
        writer.Write(SessionId);
        writer.Write(Type);
        writer.WriteBytes(Data);
    }

    public void FromReader(PacketReader reader)
    {
        Type = reader.Read<short>();
        reader.ReadBytes(Data); // TODO
    }
}