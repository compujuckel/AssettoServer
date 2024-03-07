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
        
        foreach(var (sessionId, result) in Results.OrderBy(r => r.Value.BestLap))
        {
            writer.Write(sessionId);
            if (IsRace)
                writer.Write(result.TotalTime);
            else 
                writer.Write(result.BestLap);
            writer.Write(result.NumLaps);
        }
        
        
        writer.Write(PickupMode);
    }
}
