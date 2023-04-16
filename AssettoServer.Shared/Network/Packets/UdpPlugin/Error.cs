using AssettoServer.Shared.Network.Packets.Outgoing;

namespace AssettoServer.Shared.Network.Packets.UdpPlugin;

public readonly record struct Error : IOutgoingNetworkPacket
{
    public string? Message { get; init; }

    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)UdpPluginProtocol.Error);
        writer.WriteUTF32String(Message);
    }
}
