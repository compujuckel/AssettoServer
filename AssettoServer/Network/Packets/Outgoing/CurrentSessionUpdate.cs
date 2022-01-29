using System;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using System.Collections.Generic;

namespace AssettoServer.Network.Packets.Outgoing
{
    public class CurrentSessionUpdate : IOutgoingNetworkPacket
    {
        public SessionConfiguration? CurrentSession;
        public float TrackGrip;
        public IEnumerable<EntryCar>? Grid;
        public long StartTime;

        public void ToWriter(ref PacketWriter writer)
        {
            if (CurrentSession == null)
                throw new ArgumentNullException(nameof(CurrentSession));
            if (Grid == null)
                throw new ArgumentNullException(nameof(Grid));

            writer.Write<byte>(0x4A);
            writer.WriteASCIIString(CurrentSession.Name);
            writer.Write((byte)CurrentSession.Id);
            writer.Write((byte)CurrentSession.Type);
            writer.Write((ushort)CurrentSession.Time);
            writer.Write((ushort)CurrentSession.Laps);
            writer.Write(TrackGrip);

            foreach (EntryCar car in Grid)
                writer.Write(car.SessionId);

            writer.Write(StartTime);
        }
    }
}
