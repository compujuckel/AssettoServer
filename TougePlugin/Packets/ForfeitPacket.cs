using AssettoServer.Network.ClientMessages;

namespace TougePlugin.Packets;

[OnlineEvent(Key = "AS_Forfeit")]
public class ForfeitPacket : OnlineEvent<ForfeitPacket>
{
    [OnlineEventField(Name = "forfeit")]
    public bool Forfeit = true;
}
