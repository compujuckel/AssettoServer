using System.Numerics;
using AssettoServer.Network.ClientMessages;

namespace FastTravelPlugin.Packets;

[OnlineEvent(Key = "AS_FastTravel")]
public class FastTravelPacket : OnlineEvent<FastTravelPacket>
{
    [OnlineEventField(Name = "position")]
    public Vector3 Position;
    [OnlineEventField(Name = "direction")]
    public Vector3 Direction;
}
