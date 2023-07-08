using System;
using System.Text.RegularExpressions;

namespace AssettoServer.Server.Configuration;

public partial class CSPTrackOptions
{
    public uint? MinimumCSPVersion { get; init; }
    public required string Track { get; init; }
    public TrackOptionsFlags Flags { get; init; }
    
    [GeneratedRegex(@"^csp\/(\d+)\/\.\.(?:\/(\w+)\/\.\.)?\/(.+)$")]
    private static partial Regex TrackOptionsRegex();

    public static CSPTrackOptions Parse(string track)
    {
        var match = TrackOptionsRegex().Match(track);
        if (match.Success)
        {
            var flags = TrackOptionsFlags.None;
            if (match.Groups[2].Success)
            {
                const int offset = 65; // A
                flags = (TrackOptionsFlags)match.Groups[2].Value[0] - offset;
            }
            
            return new CSPTrackOptions
            {
                MinimumCSPVersion = uint.Parse(match.Groups[1].Value),
                Track = match.Groups[3].Value,
                Flags = flags
            };
        }

        return new CSPTrackOptions
        {
            Track = track
        };
    }
}

[Flags]
public enum TrackOptionsFlags
{
    None = 0,
    CustomCarPhysics = 1,
    CustomTrackPhysics = 2,
    HidePitCrew = 4
}
