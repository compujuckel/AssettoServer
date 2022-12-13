using System;
using AssettoServer.Server.Ai.Structs;

namespace AssettoServer.Server.Ai;

public class TrafficSpline
{
    public string? Name { get; init; }
    public SplinePointStruct[] Points { get; init; } = Array.Empty<SplinePointStruct>();
    public float MinRadius { get; init; }
}
