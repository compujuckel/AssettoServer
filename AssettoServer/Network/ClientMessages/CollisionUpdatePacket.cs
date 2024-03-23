using System.Numerics;

namespace AssettoServer.Network.ClientMessages;

[OnlineEvent(Key = "AS_CollisionUpdate")]
public class CollisionUpdatePacket : OnlineEvent<CollisionUpdatePacket>
{
    [OnlineEventField(Name = "enabled")]
    public bool Enabled;
    [OnlineEventField(Name = "target")]
    public byte Target;
}
