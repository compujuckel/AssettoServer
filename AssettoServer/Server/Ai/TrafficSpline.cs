using System.Numerics;

namespace AssettoServer.Server.Ai
{
    public class TrafficSpline
    {
        public string Name { get; }
        public TrafficSplinePoint[] Points { get; }

        public TrafficSpline(string name, TrafficSplinePoint[] points)
        {
            Name = name;
            Points = points;
        }
    }
}