using ReplayPlugin.Utils;

namespace ReplayPlugin.Data;

public class KunosReplayHeader
{
    public const uint Version = 16;
    public double RecordingIntervalMs;
    public string? Weather;
    public string? Track;
    public string? TrackConfiguration;
    public uint CarsNumber;
    public uint CurrentRecordingIndex;
    public uint RecordedFrames;
    public uint TrackObjectsNumber;

    public void ToWriter(BinaryWriter writer)
    {
        writer.Write(Version);
        writer.Write(RecordingIntervalMs);
        writer.WriteACString(Weather);
        writer.WriteACString(Track);
        writer.WriteACString(TrackConfiguration);
        
        writer.Write(CarsNumber);
        writer.Write(CurrentRecordingIndex);
        
        writer.Write(RecordedFrames);
        writer.Write(TrackObjectsNumber);
    }
}
