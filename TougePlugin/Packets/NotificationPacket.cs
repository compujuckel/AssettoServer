using AssettoServer.Network.ClientMessages;

namespace TougePlugin.Packets;

[OnlineEvent(Key = "AS_Notification")]
public class NotificationPacket : OnlineEvent<NotificationPacket>
{
    [OnlineEventField(Name = "message", Size = 64)]
    public string Message;
}
