namespace AssettoServer.Shared.Network.Packets.Outgoing.Handshake;

public readonly struct NoSlotsAvailableResponse : IOutgoingNetworkPacket
{
    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)ACServerProtocol.NoSlotsAvailable);
    }
}
