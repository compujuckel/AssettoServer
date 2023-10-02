namespace nvrlift.AssettoServer.Track;

public class TrackData
{
    public TrackType? Type { get; set; }
    public TrackType? UpcomingType { get; set; }
    public double TransitionDuration { get; set; }
    public bool UpdateContentManager { get; set; }
    public TrackData(TrackType? type, TrackType? upcomingType)
    {
        Type = type;
        UpcomingType = upcomingType;
    }
}
