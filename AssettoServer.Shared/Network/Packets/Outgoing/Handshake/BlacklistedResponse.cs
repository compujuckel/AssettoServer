namespace AssettoServer.Shared.Network.Packets.Outgoing.Handshake;

public readonly struct BlacklistedResponse : IOutgoingNetworkPacket
{
    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)ACServerProtocol.Blacklisted);
    }
}
