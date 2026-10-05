using AssettoServer.Network.ClientMessages;

namespace AutoModerationPlugin.Packets;

[OnlineEvent(Key = "autoModerationFlag")]
public class AutoModerationFlags : OnlineEvent<AutoModerationFlags>
{
    [OnlineEventField(Name = "flags")]
    public Flags Flags;
}

[Flags]
public enum Flags : byte
{
    NoLights = 1,
    NoParking = 2,
    WrongWay = 4
}
