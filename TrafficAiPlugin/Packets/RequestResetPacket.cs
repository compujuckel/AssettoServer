using AssettoServer.Network.ClientMessages;

namespace TrafficAiPlugin.Packets;

[OnlineEvent(Key = "AS_RequestResetCar")]
public class RequestResetPacket : OnlineEvent<RequestResetPacket>
{
    [OnlineEventField(Name = "dummy")]
    public byte Dummy;
}
