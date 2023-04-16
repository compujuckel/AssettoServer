namespace AssettoServer.Shared.Network.Packets.Outgoing.Handshake;

public readonly struct AuthFailedResponse : IOutgoingNetworkPacket
{
    public readonly string Reason;

    public AuthFailedResponse(string reason)
    {
        Reason = reason;
    }

    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)ACServerProtocol.AuthFailed);
        writer.WriteUTF32String(Reason);
    }
}
