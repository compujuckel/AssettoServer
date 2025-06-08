namespace AssettoServer.Network.ClientMessages;

[OnlineEvent(Key = "AS_LuaReady")]
public class LuaReadyPacket : OnlineEvent<LuaReadyPacket>
{
    [OnlineEventField(Name = "dummy")]
    public byte Dummy;
}
