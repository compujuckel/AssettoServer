using System.Numerics;

namespace AssettoServer.Server.Ai.Structs;

public struct SplinePointStruct
{
    public int Id;
    public Vector3 Position;
    public float Radius;
    public float Camber;
    public float Length;

    public int JunctionStartId;
    public int JunctionEndId;
    public int PreviousId;
    public int NextId;
    public int LeftId;
    public int RightId;
}
