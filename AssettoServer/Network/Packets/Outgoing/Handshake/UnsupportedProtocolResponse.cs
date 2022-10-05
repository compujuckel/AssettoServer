namespace AssettoServer.Network.Packets.Outgoing.Handshake;

public readonly struct UnsupportedProtocolResponse : IOutgoingNetworkPacket
{
    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)ACServerProtocol.UnsupportedProtocol);
        writer.Write((ushort)202);
    }
}
