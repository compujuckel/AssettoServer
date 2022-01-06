using System.Drawing;
using System.IO;
using AssettoServer.Network.Packets.Outgoing;

namespace AssettoServer.Network.Packets.Shared;

public class CSPCarColorUpdate : CSPClientMessageOutgoing
{
    public Color Color { get; init; }

    public CSPCarColorUpdate()
    {
        Type = CSPClientMessageType.CarColorChange;
    }

    protected override void ToWriter(BinaryWriter writer)
    {
        writer.Write(Color.R);
        writer.Write(Color.G);
        writer.Write(Color.B);
        writer.Write(Color.A);
    }
}