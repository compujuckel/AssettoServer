using System.Numerics;

namespace AssettoServer.Network.ClientMessages;

[OnlineEvent(Key = "AS_ResetCar")]
public class ResetCarPacket : OnlineEvent<ResetCarPacket>
{
    [OnlineEventField(Name = "closest")]
    public Vector3 Closest;
    [OnlineEventField(Name = "next")]
    public Vector3 Next;
}
