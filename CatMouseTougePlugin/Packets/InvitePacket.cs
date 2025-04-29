using AssettoServer.Network.ClientMessages;

namespace CatMouseTougePlugin.Packets;

[OnlineEvent(Key = "AS_Invite")]
public class InvitePacket : OnlineEvent<InvitePacket>
{
    [OnlineEventField(Name = "inviteSenderName", Size = 32)]
    public string InviteSenderName;
}
