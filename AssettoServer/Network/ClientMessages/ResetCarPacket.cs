using System.Numerics;

namespace AssettoServer.Network.ClientMessages;

[OnlineEvent(Key = "AS_ResetCar")]
public class ResetCarPacket : OnlineEvent<ResetCarPacket>
{
    [OnlineEventField(Name = "closest")]
    public Vector3 Closest;
    [OnlineEventField(Name = "direction")]
    public Vector3 Direction;
}
