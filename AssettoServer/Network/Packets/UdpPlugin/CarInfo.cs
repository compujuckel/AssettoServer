using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AssettoServer.Network.Packets.Outgoing;

namespace AssettoServer.Network.Packets.UdpPlugin
{
    public class CarInfo : IOutgoingNetworkPacket
    {
        public byte CarId;
        public bool IsConnected;
        public string? Model;
        public string? Skin;
        public string? DriverName;
        public string? DriverTeam;
        public string? DriverGuid;

        public void ToWriter(ref PacketWriter writer)
        {
            writer.Write((byte)UdpPluginProtocol.CarInfo);
            writer.Write(CarId);
            writer.Write(IsConnected);
            writer.WriteUTF32String(Model);
            writer.WriteUTF32String(Skin);
            writer.WriteUTF32String(DriverName);
            writer.WriteUTF32String(DriverTeam);
            writer.WriteUTF32String(DriverGuid);
        }
    }
}
