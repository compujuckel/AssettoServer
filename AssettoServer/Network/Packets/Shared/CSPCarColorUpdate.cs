using System;
using System.Drawing;
using System.IO;
using AssettoServer.Network.Packets.Incoming;
using AssettoServer.Network.Packets.Outgoing;

namespace AssettoServer.Network.Packets.Shared
{
    public class CSPCarColorUpdate : ChatEncodedPacket
    {
        public Color Color { get; init; }

        protected override void ToWriter(BinaryWriter writer)
        {
            writer.Write((byte)0x98);
            writer.Write((byte)0x3A);
            writer.Write(Color.R);
            writer.Write(Color.G);
            writer.Write(Color.B);
            writer.Write(Color.A);
        }
    }
}