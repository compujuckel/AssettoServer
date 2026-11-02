using System.Runtime.InteropServices;

namespace AssettoServer.Server.Ai.Splines;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly struct AiSplineHeader(int version, float laneWidthMeters, bool twoWayTraffic, int numPoints, int numJunctions, int numKdTreePoints)
{
    public readonly int Version = version;
    public readonly float LaneWidthMeters = laneWidthMeters;
    public readonly bool TwoWayTraffic = twoWayTraffic;
    public readonly int NumPoints = numPoints;
    public readonly int NumJunctions = numJunctions;
    public readonly int NumKdTreePoints = numKdTreePoints;
}
