namespace AssettoServer.Network.Packets.Outgoing;

public class KickCar : IOutgoingNetworkPacket
{
    public byte SessionId;
    public KickReason Reason;

    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)ACServerProtocol.KickCar);
        writer.Write(SessionId);
        writer.Write(Reason);
    }
}

public enum KickReason : byte
{
    VoteKicked,
    VoteBanned,
    VoteBlacklisted,
    ChecksumFailed,
    Kicked
}
