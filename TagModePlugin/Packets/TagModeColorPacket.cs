using AssettoServer.Network.ClientMessages;

namespace TagModePlugin.Packets;

[OnlineEvent(Key = "tagModeColorPacket")]
public class TagModeColorPacket : OnlineEvent<TagModeColorPacket>
{
    [OnlineEventField(Name = "R")]
    public byte R;
    [OnlineEventField(Name = "G")]
    public byte G;
    [OnlineEventField(Name = "B")]
    public byte B;
    [OnlineEventField(Name = "Target")]
    public byte Target;
    [OnlineEventField(Name = "Disconnect")]
    public bool Disconnect;
}
