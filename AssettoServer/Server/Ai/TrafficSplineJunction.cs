using AssettoServer.Network.Packets.Outgoing;

namespace AssettoServer.Server.Ai;

public class TrafficSplineJunction
{
    public int Id { get; set; }
    public TrafficSplinePoint? StartPoint { get; set; }
    public TrafficSplinePoint? EndPoint { get; set; }
    public float Probability { get; set; }
    public CarStatusFlags IndicateWhenTaken { get; set; }
    public CarStatusFlags IndicateWhenNotTaken { get; set; }
    public float IndicateDistancePre { get; set; }
    public float IndicateDistancePost { get; set; }
}
