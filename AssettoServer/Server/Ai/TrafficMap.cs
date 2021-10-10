using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace AssettoServer.Server.Ai
{
    public class TrafficMap
    {
        public string SourcePath { get; }
        public List<TrafficSpline> Splines { get; }
        
        public Dictionary<int, TrafficSplinePoint> PointsById { get; }

        public TrafficMap(string sourcePath, List<TrafficSpline> splines)
        {
            SourcePath = sourcePath;
            Splines = splines;
            PointsById = new Dictionary<int, TrafficSplinePoint>();

            foreach (var point in splines.SelectMany(spline => spline.Points))
            {
                PointsById.Add(point.Id, point);
            }
            
            AdjacentLaneDetector.GetAdjacentLanesForMap(this, SourcePath + ".lanes");
            JunctionParser.Parse(this, SourcePath + ".junctions.csv");
        }

        public TrafficMapView NewView()
        {
            return new TrafficMapView(this);
        }

        public (TrafficSplinePoint point, float distanceSquared) WorldToSpline(Vector3 position)
        {
            TrafficSplinePoint minPoint = null;
            float minDistance = float.MaxValue;
            foreach (var spline in Splines)
            {
                foreach (var point in spline.Points)
                {
                    float dist = Vector3.DistanceSquared(position, point.Point);
                    if (dist < minDistance)
                    {
                        minPoint = point;
                        minDistance = dist;
                    }
                }
            }

            return (point: minPoint, distanceSquared: minDistance);
        }
    }
}