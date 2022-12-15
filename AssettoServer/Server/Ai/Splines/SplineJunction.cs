using System.Runtime.InteropServices;
using AssettoServer.Network.Packets.Outgoing;

namespace AssettoServer.Server.Ai.Splines;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct SplineJunction
{
    public int Id;
    public int StartPointId;
    public int EndPointId;
    public float Probability;
    public CarStatusFlags IndicateWhenTaken;
    public CarStatusFlags IndicateWhenNotTaken;
    public float IndicateDistancePre;
    public float IndicateDistancePost;
}
