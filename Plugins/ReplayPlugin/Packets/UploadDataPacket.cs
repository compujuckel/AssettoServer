using System.Numerics;
using AssettoServer.Network.ClientMessages;

namespace ReplayPlugin.Packets;

[OnlineEvent(Key = "ReplayPlugin_uploadData")]
public class UploadDataPacket : OnlineEvent<UploadDataPacket>
{
    [OnlineEventField(Name = "sessionID")]
    public byte CarId;
    [OnlineEventField(Name = "wheelPositions", Size = 4)]
    public Vector3[] WheelPositions = null!;
}
