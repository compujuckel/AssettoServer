using System.Numerics;

namespace FastLaneUtils;

public class SplinePoint
{
    public Vector3 Position { get; set; }
    public float Length { get; set; }
    public int Id { get; set; }
    
    public float Speed { get; set; }
    public float Gas { get; set; }
    public float Brake { get; set; }
    public float ObsoleteLatG { get; set; }
    public float Radius { get; set; }
    public float SideLeft { get; set; }
    public float SideRight { get; set; }
    public float Camber { get; set; }
    public float Direction { get; set; }
    public Vector3 Normal { get; set; }
    public float DetailLength { get; set; }
    public Vector3 Forward { get; set; }
    public float Tag { get; set; }
    public float Grade { get; set; }
}