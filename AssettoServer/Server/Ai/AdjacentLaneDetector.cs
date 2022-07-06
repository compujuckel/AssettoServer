using System;
using System.Numerics;
using System.Threading.Tasks;
using Serilog;
using SerilogTimings;

namespace AssettoServer.Server.Ai
{
    public static class AdjacentLaneDetector
    {
        private const float LaneDetectionRadius = 2.0f;

        private static Vector3 OffsetVec(Vector3 pos, float angle, float offset)
        {
            return new()
            {
                X = (float) (pos.X + Math.Cos(angle * Math.PI / 180) * offset),
                Y = pos.Y,
                Z = (float) (pos.Z - Math.Sin(angle * Math.PI / 180) * offset)
            };
        }

        public static void DetectAdjacentLanes(TrafficMap map, float laneWidth, bool twoWayTraffic)
        {
            const float minRadius = LaneDetectionRadius * 1.05f;
            if (laneWidth < minRadius)
            {
                throw new InvalidOperationException($"Lane width cannot be smaller than {minRadius} meters");
            }
            
            Log.Information("Adjacent lane detection...");
            
            using var t = Operation.Time("Adjacent lane detection");

            foreach (var spline in map.Splines)
            {
                Parallel.ForEach(spline.Value.Points, point =>
                {
                    if (point.Right == null && point.Next != null)
                    {
                        float direction = (float) (Math.Atan2(point.Position.Z - point.Next.Position.Z, point.Next.Position.X - point.Position.X) * (180 / Math.PI) * -1);

                        var targetVec = OffsetVec(point.Position, -direction + 90, laneWidth);

                        var found = map.WorldToSpline(targetVec);
                        if (found.Point != null && found.DistanceSquared < LaneDetectionRadius * LaneDetectionRadius)
                        {
                            point.Left = found.Point;
                            if (point.IsSameDirection(found.Point))
                            {
                                found.Point.Right = point;
                            }
                            else
                            {
                                found.Point.Left = point;
                            }
                        }
                        
                        targetVec = OffsetVec(point.Position, -direction - 90, laneWidth);

                        found = map.WorldToSpline(targetVec);
                        if (found.Point != null && found.DistanceSquared < LaneDetectionRadius * LaneDetectionRadius)
                        {
                            point.Right = found.Point;
                            if (point.IsSameDirection(found.Point))
                            {
                                found.Point.Left = point;
                            }
                            else
                            {
                                found.Point.Right = point;
                            }
                        }
                    }
                });
            }
            
            foreach (var point in map.PointsById.Values)
            {
                if (point.Lanes.Length == 0)
                {
                    var lanes = point.GetLanes(twoWayTraffic).ToArray();
                    foreach (var lane in lanes)
                    {
                        lane.Lanes = lanes;
                    }
                }
            }
        }
    }
}
