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

    private const int FlagsOffset = 65; // 'A'

    public static CSPTrackOptions Parse(string track, uint? minimumCspVersion = null)
    {
        var match = TrackOptionsRegex().Match(track);
        if (match.Success)
        {
            var flags = TrackOptionsFlags.None;
            if (match.Groups[2].Success)
            {
                flags = (TrackOptionsFlags)match.Groups[2].Value[0] - FlagsOffset;
            }
            
            return new CSPTrackOptions
            {
                MinimumCSPVersion = Math.Max(uint.Parse(match.Groups[1].Value), minimumCspVersion ?? 0),
                Track = match.Groups[3].Value,
                Flags = flags
            };
        }

        return new CSPTrackOptions
        {
            Track = track,
            MinimumCSPVersion = minimumCspVersion
        };
    }

    public override string ToString()
    {
        if (!MinimumCSPVersion.HasValue)
        {
            return Track;
        }

        var flags = Flags != TrackOptionsFlags.None
            ? $"/{(char)(Flags + FlagsOffset)}/.."
            : "";

        return $"csp/{MinimumCSPVersion.Value}/..{flags}/{Track}";
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
