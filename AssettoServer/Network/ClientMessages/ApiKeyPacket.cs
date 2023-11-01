namespace AssettoServer.Network.ClientMessages;

// 0x27612FAB
[OnlineEvent(Key = "AS_ApiKey")]
public class ApiKeyPacket : OnlineEvent<ApiKeyPacket>
{
    [OnlineEventField(Name = "key", Size = 32)]
    public string Key = null!;
}
