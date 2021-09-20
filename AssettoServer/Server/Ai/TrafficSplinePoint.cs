using System.Numerics;

namespace AssettoServer.Server.Ai
{
    public class TrafficSplinePoint
    {
        public Vector3 Point { get; init; }
        public TrafficSplinePointJunction Junction { get; set; } = null;

        public TrafficSplinePoint Previous { get; set; } = null;
        public TrafficSplinePoint Next { get; set; } = null;

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