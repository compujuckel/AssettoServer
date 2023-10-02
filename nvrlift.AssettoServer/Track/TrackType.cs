namespace nvrlift.AssettoServer.Track;

public class TrackType
{
    public required string Name { get; init; }
    public required string TrackFolder { get; init; }
    public required string TrackLayoutConfig { get; init; }
    public float Weight { get; init; } = 1.0f;
    public string CMLink { get; init; } = "";
    public string CMVersion { get; init; } = "";
}
