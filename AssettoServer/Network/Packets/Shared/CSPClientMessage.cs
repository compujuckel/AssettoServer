using AssettoServer.Network.Packets.Incoming;
using AssettoServer.Network.Packets.Outgoing;

namespace AssettoServer.Network.Packets.Shared;

public struct CSPClientMessage : IOutgoingNetworkPacket, IIncomingNetworkPacket
{
    public byte SessionId;
    public ushort Type;
    public int? LuaType;
    public byte[] Data;
    
    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write<byte>(0xAB);
        writer.Write<byte>(0x03);
        writer.Write(SessionId);
        writer.Write(Type);
        if(LuaType.HasValue)
            writer.Write(LuaType.Value);
        writer.WriteBytes(Data);
    }

    public void FromReader(PacketReader reader)
    {
        Data = new byte[reader.Buffer.Length - reader.ReadPosition];
        reader.ReadBytes(Data);
    }
}