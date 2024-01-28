using System.Numerics;

namespace AssettoServer.Network.ClientMessages;

[OnlineEvent(Key = "AS_TeleportCar")]
public class TeleportCarPacket : OnlineEvent<TeleportCarPacket>
{
    [OnlineEventField(Name = "closest")]
    public Vector3 Closest;
    [OnlineEventField(Name = "direction")]
    public Vector3 Direction;
    [OnlineEventField(Name = "velocity")]
    public Vector3 Velocity;
}
