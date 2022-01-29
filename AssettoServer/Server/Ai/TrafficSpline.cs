using System;
using System.Numerics;

namespace AssettoServer.Server.Ai
{
    public class TrafficSpline
    {
        public string? Name { get; init; }
        public TrafficSplinePoint[] Points { get; init; } = Array.Empty<TrafficSplinePoint>();
        public float MinCorneringSpeed { get; init; }
    }
}