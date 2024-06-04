using AssettoServer.Shared.Model;

namespace AssettoServer.Shared.Network.Packets.Outgoing;

public class CarListResponse : IOutgoingNetworkPacket
{
    public int PageIndex;
    public int EntryCarsCount;
    public required IEnumerable<IEntryCar<IClient>> EntryCars;
    public Dictionary<byte, EntryCarResult> CarResults;

    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)ACServerProtocol.CarList);
        writer.Write((byte)PageIndex);
        writer.Write((byte)EntryCarsCount);
        foreach (var car in EntryCars)
        {
            writer.Write(car.SessionId);
            writer.WriteUTF8String(car.Model);
            writer.WriteUTF8String(car.Skin);
            writer.WriteUTF8String(CarResults[car.SessionId].Name);
            writer.WriteUTF8String(CarResults[car.SessionId].Team);
            writer.WriteUTF8String(CarResults[car.SessionId].NationCode);
            writer.Write(car.IsSpectator);
            writer.Write(car.Status.DamageZoneLevel);
        }
    }
}
