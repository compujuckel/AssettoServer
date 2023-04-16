using AssettoServer.Shared.Model;

namespace AssettoServer.Shared.Network.Packets.Outgoing;

public class CurrentSessionUpdate : IOutgoingNetworkPacket
{
    public ISession? CurrentSession;
    public float TrackGrip;
    public IEnumerable<IEntryCar<IClient>>? Grid;
    public long StartTime;

    public void ToWriter(ref PacketWriter writer)
    {
        if (CurrentSession == null)
            throw new ArgumentNullException(nameof(CurrentSession));
        if (Grid == null)
            throw new ArgumentNullException(nameof(Grid));

        writer.Write((byte)ACServerProtocol.CurrentSessionUpdate);
        writer.WriteASCIIString(CurrentSession.Name);
        writer.Write((byte)CurrentSession.Id);
        writer.Write((byte)CurrentSession.Type);
        writer.Write((ushort)CurrentSession.Time);
        writer.Write((ushort)CurrentSession.Laps);
        writer.Write(TrackGrip);

        foreach (var car in Grid)
            writer.Write(car.SessionId);

        writer.Write(StartTime);
    }
}
