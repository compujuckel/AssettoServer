using System;
using System.Numerics;
using AssettoServer.Utils;

namespace AssettoServer.Server.Ai.Structs;

public readonly ref struct SplinePointOperations
{
    public Span<SplinePointStruct> Points { get; }

    public SplinePointOperations(Span<SplinePointStruct> points)
    {
        Points = points;
    }
    
    public Vector3 GetForwardVector(int pointId)
    {
        var point = Points[pointId];
        
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
}
