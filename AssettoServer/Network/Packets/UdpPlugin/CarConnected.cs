using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AssettoServer.Network.Packets.Outgoing;

namespace AssettoServer.Network.Packets.UdpPlugin
{
    public readonly record struct CarConnected : IOutgoingNetworkPacket
    {
        public string? DriverName { get; init; }
        public string? DriverGuid { get; init; }
        public byte SessionId { get; init; }
        public string? CarModel { get; init; }
        public string? CarSkin { get; init; }

        public void ToWriter(ref PacketWriter writer)
        {
            writer.Write((byte)UdpPluginProtocol.NewConnection);
            writer.WriteString(DriverName, Encoding.UTF32);
            writer.WriteString(DriverGuid, Encoding.UTF32);
            writer.Write(SessionId);
            writer.WriteString(CarModel, Encoding.UTF8);
            writer.WriteString(CarSkin, Encoding.UTF8);
        }
    }
}
