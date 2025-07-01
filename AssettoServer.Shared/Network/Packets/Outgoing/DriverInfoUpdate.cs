using AssettoServer.Shared.Model;

namespace AssettoServer.Shared.Network.Packets.Outgoing;

public class DriverInfoUpdate : IOutgoingNetworkPacket
{
    public required IEnumerable<IEntryCar> ConnectedCars { get; init; }

    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)ACServerProtocol.DriverInfoUpdate);
        writer.Write((byte)ConnectedCars.Count());

        foreach(var car in ConnectedCars)
        {
            writer.Write(car.SessionId);
            writer.WriteUTF32String(car.AiControlled ? car.AiName : car.Client?.Name);
        }
    }
}
