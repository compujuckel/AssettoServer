using System.Drawing;
using AssettoServer.Network.ClientMessages;

namespace TagModePlugin.Packets;

[OnlineEvent(Key = "tagModeColorPacket")]
public class TagModeColorPacket : OnlineEvent<TagModeColorPacket>
{
    [OnlineEventField(Name = "Color")]
    public Color Color;
    [OnlineEventField(Name = "Disconnect")]
    public bool Disconnect;
}
