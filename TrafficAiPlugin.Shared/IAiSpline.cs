using System.Numerics;
using TrafficAiPlugin.Shared.Splines;

namespace TrafficAiPlugin.Shared;

public interface IAiSpline
{
    public SplinePointOperations Operations { get; }
    public (int PointId, float DistanceSquared) WorldToSpline(Vector3 position);
}
