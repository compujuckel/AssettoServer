using System;

namespace AssettoServer.Server.Ai.Splines;

public class FastLane
{
    public string? Name { get; init; }
    public SplinePoint[] Points { get; init; } = Array.Empty<SplinePoint>();
}
