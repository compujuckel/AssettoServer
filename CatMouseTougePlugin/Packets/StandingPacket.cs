using AssettoServer.Network.ClientMessages;

namespace CatMouseTougePlugin.Packets;

[OnlineEvent(Key = "AS_Standing")]
public class StandingPacket : OnlineEvent<StandingPacket>
{
    [OnlineEventField(Name = "result1")]
    public int Result1;
    [OnlineEventField(Name = "result2")]
    public int Result2;
    [OnlineEventField(Name = "result3")]
    public int Result3;
    [OnlineEventField(Name = "isHudOn")]
    public bool IsHudOn = true;
}
