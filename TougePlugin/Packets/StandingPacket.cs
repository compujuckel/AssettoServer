using AssettoServer.Network.ClientMessages;

namespace TougePlugin.Packets;

[OnlineEvent(Key = "AS_Standing")]
public class StandingPacket : OnlineEvent<StandingPacket>
{
    [OnlineEventField(Name = "result1")]
    public int Result1;
    [OnlineEventField(Name = "result2")]
    public int Result2;
    [OnlineEventField(Name = "suddenDeathResult")]
    public int SuddenDeathResult;
    [OnlineEventField(Name = "hudState")]
    public int HudState;
}
