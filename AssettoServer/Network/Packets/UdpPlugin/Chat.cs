using AssettoServer.Network.Packets.Outgoing;

namespace AssettoServer.Network.Packets.UdpPlugin;

public readonly record struct Chat : IOutgoingNetworkPacket
{
    public byte SessionId { get; init; }
    public string? Message { get; init; }

    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)UdpPluginProtocol.Chat);
        writer.Write(SessionId);
        writer.WriteUTF32String(Message);
    }
}
