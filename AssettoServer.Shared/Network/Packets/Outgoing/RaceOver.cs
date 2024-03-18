using AssettoServer.Shared.Model;

namespace AssettoServer.Shared.Network.Packets.Outgoing;

public class RaceOver : IOutgoingNetworkPacket
{
    public bool PickupMode;
    public bool IsRace;
    public required Dictionary<byte, EntryCarResult> Results;
    
    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)ACServerProtocol.RaceOver);
        
        foreach(var (sessionId, result) in Results) // .OrderBy(r => IsRace ? r.Value.TotalTime : r.Value.BestLap)
        {
            writer.Write(sessionId);
            writer.Write((uint)(IsRace ? result.TotalTime : result.BestLap));
            writer.Write((ushort)result.NumLaps);
        }
        
        writer.Write(PickupMode);
    }
}
