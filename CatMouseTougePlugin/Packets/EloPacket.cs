using AssettoServer.Network.ClientMessages;

namespace TougePlugin.Packets;

[OnlineEvent(Key = "AS_Elo")]
public class EloPacket : OnlineEvent<EloPacket>
{
    [OnlineEventField(Name = "elo")]
    public int Elo;
}
