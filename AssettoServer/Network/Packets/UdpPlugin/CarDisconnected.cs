using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AssettoServer.Network.Packets.Outgoing;

namespace AssettoServer.Network.Packets.UdpPlugin
{
    public class CarDisconnected : IOutgoingNetworkPacket
    {
        public string? DriverName;
        public string? DriverGuid;
        public byte SessionId;
        public string? CarModel;
        public string? CarSkin;

        public void ToWriter(ref PacketWriter writer)
        {
            writer.Write((byte)UdpPluginProtocol.ClosedConnection);
            writer.WriteString(DriverName, Encoding.UTF32);
            writer.WriteString(DriverGuid, Encoding.UTF32);
            writer.Write(SessionId);
            writer.WriteString(CarModel, Encoding.UTF8);
            writer.WriteString(CarSkin, Encoding.UTF8);
        }
    }
}
