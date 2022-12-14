using System;
using System.Collections.Generic;
using System.Numerics;
using AssettoServer.Utils;
using Serilog;

namespace AssettoServer.Server.Ai.Structs;

public readonly ref struct SplinePointOperations
{
    public ReadOnlySpan<SplinePoint> Points { get; }

    public SplinePointOperations(ReadOnlySpan<SplinePoint> points)
    {
        Points = points;
    }
    
    public Vector3 GetForwardVector(int pointId)
    {
        ref readonly var point = ref Points[pointId];
        
        if (point.NextId >= 0)
        {
            return Points[point.NextId].Position - point.Position;
        }

        return Vector3.Zero;
    }
    
    public bool IsSameDirection(int pointId1, int pointId2)
    {
        if (pointId1 < 0 || pointId2 < 0) return false;
        return Vector3.Dot(GetForwardVector(pointId1), GetForwardVector(pointId2)) > 0;
    }
    
    public float GetCamber(int pointId, float lerp = 0)
    {
        var point = Points[pointId];
        float camber = point.Camber;
        if (lerp != 0 && point.NextId >= 0)
        {
            camber = (float)MathUtils.Lerp(camber, GetCamber(point.NextId), lerp);
        }

        return camber;
    }
    
    public List<int> GetLanes(int startPointId, bool twoWayTraffic = false)
    {
        var ret = new List<int>();
        const int maxCount = 10;

        int point = Points[startPointId].LeftId;
        while (point >= 0 && ret.Count < maxCount)
        {
            if (IsSameDirection(startPointId, point))
            {
                if (ret.Contains(point)) break;
                
                ret.Add(point);
                point = Points[point].LeftId;
            }
            else if (twoWayTraffic)
            {
                if (ret.Contains(point)) break;
                
                ret.Add(point);
                point = Points[point].RightId;
            }
            else
            {
                break;
            }
        }
            
        ret.Reverse();
        ret.Add(startPointId);

        point = Points[startPointId].RightId;
        while (point >= 0 && ret.Count < maxCount)
        {
            if (IsSameDirection(startPointId, point))
            {
                if (ret.Contains(point)) break;
                
                ret.Add(point);
                point = Points[point].RightId;
            }
            else if (twoWayTraffic)
            {
                if (ret.Contains(point)) break;
                
                ret.Add(point);
                point = Points[point].LeftId;
            }
            else
            {
                break;
            }
        }

        if (ret.Count >= maxCount)
        {
            Log.Debug("Possible loop at AI spline point {SplinePointId}", startPointId);
        }

        return ret;
    }
}
