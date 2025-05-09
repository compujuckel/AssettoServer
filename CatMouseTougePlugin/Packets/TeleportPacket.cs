using System.Numerics;
using AssettoServer.Network.ClientMessages;

namespace TougePlugin.Packets;

[OnlineEvent(Key = "AS_Teleport")]
public class TeleportPacket : OnlineEvent<TeleportPacket>
{
    [OnlineEventField(Name = "position")]
    public Vector3 Position;
    [OnlineEventField(Name = "direction")]
    public Vector3 Direction;
}
