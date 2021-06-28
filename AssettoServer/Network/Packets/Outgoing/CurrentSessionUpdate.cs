using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssettoServer.Network.Packets.Outgoing
{
    public class CurrentSessionUpdate : IOutgoingNetworkPacket
    {
        public EntryCar TargetCar;
        public SessionConfiguration CurrentSession;
        public float TrackGrip;
        public IEnumerable<EntryCar> ConnectedCars;

        public void ToWriter(ref PacketWriter writer)
        {
            writer.Write<byte>(0x4A);
            writer.WriteASCIIString(CurrentSession.Name);
            writer.Write((byte)CurrentSession.Id);
            writer.Write((byte)CurrentSession.Type);
            writer.Write((ushort)CurrentSession.Time);
            writer.Write((ushort)CurrentSession.Laps);
            writer.Write(TrackGrip);

            foreach (EntryCar car in ConnectedCars)
                writer.Write(car.SessionId);

            writer.Write<long>(CurrentSession.StartTimeTicks - TargetCar.TimeOffset);
        }
    }
}
