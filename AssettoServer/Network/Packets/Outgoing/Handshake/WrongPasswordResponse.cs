namespace AssettoServer.Network.Packets.Outgoing.Handshake;

public readonly struct WrongPasswordResponse : IOutgoingNetworkPacket
{
    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)ACServerProtocol.WrongPassword);
    }
}
