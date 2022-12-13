using AssettoServer.Network.Packets.Outgoing;

namespace AssettoServer.Server.Ai.Structs;

public struct SplineJunctionStruct
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
