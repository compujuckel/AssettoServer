using System.Numerics;
using Serilog;
using SerilogTimings;
using TrafficAiPlugin.Shared.Splines;

namespace TrafficAiPlugin.Splines;

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

    public static void DetectAdjacentLanes(MutableAiSpline map, float laneWidth, bool twoWayTraffic)
    {
        const float minRadius = LaneDetectionRadius * 1.05f;
        if (laneWidth < minRadius)
        {
            throw new InvalidOperationException($"Lane width cannot be smaller than {minRadius} meters");
        }
            
        Log.Information("Adjacent lane detection...");
        
        using var t = Operation.Time("Adjacent lane detection");

        var spo = new SplinePointOperations(map.Points.AsSpan());

        for (int i = 0; i < spo.Points.Length; i++)
        {
            ref var point = ref map.Points[i];
            
            if (point.RightId < 0 && point.NextId >= 0)
            {
                float direction = (float) (Math.Atan2(point.Position.Z - map.Points[point.NextId].Position.Z, map.Points[point.NextId].Position.X - point.Position.X) * (180 / Math.PI) * -1);

                var targetVec = OffsetVec(point.Position, -direction + 90, laneWidth);

                var found = map.WorldToSpline(targetVec);
                if (found.PointId >= 0 && found.DistanceSquared < LaneDetectionRadius * LaneDetectionRadius)
                {
                    point.LeftId = found.PointId;
                    if (spo.IsSameDirection(point.Id, found.PointId))
                    {
                        map.Points[found.PointId].RightId = point.Id;
                    }
                    else
                    {
                        map.Points[found.PointId].LeftId = point.Id;
                    }
                }
                        
                targetVec = OffsetVec(point.Position, -direction - 90, laneWidth);

                found = map.WorldToSpline(targetVec);
                if (found.PointId >= 0 && found.DistanceSquared < LaneDetectionRadius * LaneDetectionRadius)
                {
                    point.RightId = found.PointId;
                    if (spo.IsSameDirection(point.Id, found.PointId))
                    {
                        map.Points[found.PointId].LeftId = point.Id;
                    }
                    else
                    {
                        map.Points[found.PointId].RightId = point.Id;
                    }
                }
            }
        }
    }
}
