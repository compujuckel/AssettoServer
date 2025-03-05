using System.Numerics;

namespace TrafficAiPlugin.Shared;

public interface IAiSpline
{
    public Vector3 GetForwardVector(int pointId);
    public (int PointId, float DistanceSquared) WorldToSpline(Vector3 position);
}
