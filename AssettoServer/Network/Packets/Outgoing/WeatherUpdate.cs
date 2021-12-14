using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssettoServer.Network.Packets.Outgoing
{
    public class WeatherUpdate : IOutgoingNetworkPacket
    {
        public byte Ambient;
        public byte Road;
        public string Graphics;
        public short WindSpeed;
        public short WindDirection;

        public void ToWriter(ref PacketWriter writer)
        {
            writer.Write<byte>(0x78);
            writer.Write(Ambient);
            writer.Write(Road);
            writer.WriteUTF32String(Graphics);
            writer.Write(WindSpeed);
            writer.Write(WindDirection);
        }

        public override string ToString()
        {
            return $"{nameof(Ambient)}: {Ambient}, {nameof(Road)}: {Road}, {nameof(Graphics)}: {Graphics}, {nameof(WindSpeed)}: {WindSpeed}, {nameof(WindDirection)}: {WindDirection}";
        }
    }
}
