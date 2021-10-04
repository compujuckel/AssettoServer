using System.Numerics;

namespace AssettoServer.Server.Ai
{
    public class TrafficSplinePoint
    {
        public Vector3 Point { get; init; }
        
        public TrafficSplinePoint JunctionEnd { get; set; }
        public TrafficSplinePoint Previous { get; set; }
        public TrafficSplineJunction JunctionStart { get; set; }
        public TrafficSplinePoint Next { get; set; }

        public TrafficSplinePoint Traverse(int count)
        {
            TrafficSplinePoint ret = this;
            for (int i = 0; i < count; i++)
            {
                if (ret.Next == null)
                {
                    return null;
                }

                ret = ret.Next;
            }

            return ret;
        }
    }
}