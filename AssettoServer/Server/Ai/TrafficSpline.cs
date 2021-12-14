using System.Numerics;

namespace AssettoServer.Server.Ai
{
    public class TrafficSpline
    {
        public string Name { get; init; }
        public TrafficSplinePoint[] Points { get; init; }
        public float MinCorneringSpeed { get; init; }
    }
}