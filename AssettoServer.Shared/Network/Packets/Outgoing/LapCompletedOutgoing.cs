namespace AssettoServer.Shared.Network.Packets.Outgoing;

public class LapCompletedOutgoing : IOutgoingNetworkPacket
{
    public byte SessionId;
    public uint LapTime;
    public byte Cuts;
    public CompletedLap[]? Laps;
    public float TrackGrip;

    public class CompletedLap
    {
        public byte SessionId;
        public uint LapTime;
        public ushort NumLaps;
        public byte HasCompletedLastLap;
        public byte RacePos;
    }
    
    public void ToWriter(ref PacketWriter writer)
    {
        if (Laps == null)
            throw new ArgumentNullException(nameof(Laps));

        writer.Write((byte)ACServerProtocol.LapCompleted);
        writer.Write(SessionId);
        writer.Write(LapTime);
        writer.Write(Cuts);
        writer.Write((byte)Laps.Length);
        foreach (var lap in Laps)
        {
            writer.Write(lap.SessionId);
            writer.Write(lap.LapTime);
            writer.Write(lap.NumLaps);
            writer.Write(lap.HasCompletedLastLap);
        }
        writer.Write(TrackGrip);
    }
}
