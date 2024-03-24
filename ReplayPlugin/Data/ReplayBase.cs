namespace ReplayPlugin.Data;

public class ReplayBase
{
    public uint Version; 
    public double RecordingIntervalMs;
    public string Weather;
    public string Track;
    public string TrackConfiguration;
    public uint CurrentRecordingIndex;
    public uint TrackObjectsNumber;

    public List<ReplayTrackFrame> TrackFrames = new();
    public ReplayCar[] Cars;

    public uint LeaderboardSize;

    public void ToWriter(ReplayWriter writer, uint numberArg)
    {
        writer.Write(Version);
        writer.Write(RecordingIntervalMs);
        writer.WriteString(Weather);
        writer.WriteString(Track);
        writer.WriteString(TrackConfiguration);
        
        writer.Write((uint) Cars.Length);
        writer.Write(CurrentRecordingIndex);
        
        writer.Write((uint) TrackFrames.Count);
        writer.Write(TrackObjectsNumber);
        foreach (var frame in TrackFrames)
        {
            frame.ToWriter(writer); // , TrackObjectsNumber);
        }
        
        foreach (var car in Cars)
        {
            car.ToWriter(writer, 0);
        }
        
        writer.Write(LeaderboardSize);
    }
}
