using System;
using AssettoServer.Server.Ai.Structs;

namespace AssettoServer.Server.Ai;

public class FastLane
{
    public string? Name { get; init; }
    public SplinePoint[] Points { get; init; } = Array.Empty<SplinePoint>();
}
