using AssettoServer.Shared.Network.Packets.Outgoing;

namespace AssettoServer.Shared.Network.Packets.UdpPlugin;

public readonly record struct Version : IOutgoingNetworkPacket
{
    public byte ProtocolVersion { get; init; }

    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)UdpPluginProtocol.Version);
        writer.Write(ProtocolVersion);
    }
}
