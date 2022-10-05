using AssettoServer.Server;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AssettoServer.Network.Packets.Outgoing;

public class DriverInfoUpdate : IOutgoingNetworkPacket
{
    public IEnumerable<EntryCar>? ConnectedCars;

    public void ToWriter(ref PacketWriter writer)
    {
        if (ConnectedCars == null)
            throw new ArgumentNullException(nameof(ConnectedCars));
            
        writer.Write((byte)ACServerProtocol.DriverInfoUpdate);
        writer.Write((byte)ConnectedCars.Count());

        foreach(EntryCar car in ConnectedCars)
        {
            writer.Write(car.SessionId);
            writer.WriteUTF32String(car.AiControlled ? car.AiName : car.Client?.Name);
        }
    }
}
