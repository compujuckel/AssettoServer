using AssettoServer.Network.Packets;
using AssettoServer.Network.Packets.Outgoing;

namespace AutoModerationPlugin.Packets;

public class AutoModerationFlags : IOutgoingNetworkPacket
{
    public Flags Flags { get; set; }
    
    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write<byte>(0xAB);
        writer.Write<byte>(0x03);
        writer.Write<byte>(255);
        writer.Write<ushort>(60000);
        writer.Write(0x79096D99);
        writer.Write(Flags);
    }
}

[Flags]
public enum Flags : byte
{
    NoLights = 1,
    NoParking = 2,
    WrongWay = 4
}