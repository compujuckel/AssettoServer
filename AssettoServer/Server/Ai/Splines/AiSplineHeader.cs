using System.Runtime.InteropServices;

namespace AssettoServer.Server.Ai.Splines;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct AiSplineHeader
{
    public int Version;
    public int NumPoints;
    public int NumJunctions;
    public int NumKdTreePoints;
}
