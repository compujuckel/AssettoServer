using AssettoServer.Network.ClientMessages;

namespace TougePlugin.Packets;

[OnlineEvent(Key = "AS_Invite")]
public class InvitePacket : OnlineEvent<InvitePacket>
{
    [OnlineEventField(Name = "inviteSenderName", Size = 32)]
    public string InviteSenderName;
    [OnlineEventField(Name = "inviteRecipientGuid")]
    public ulong InviteRecipientGuid = 0;
}
