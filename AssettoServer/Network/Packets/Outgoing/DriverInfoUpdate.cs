using AssettoServer.Server;
using System.Collections.Generic;
using System.Linq;

namespace AssettoServer.Network.Packets.Outgoing;

public class DriverInfoUpdate : IOutgoingNetworkPacket
{
    public required IEnumerable<EntryCar> ConnectedCars { get; init; }

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
