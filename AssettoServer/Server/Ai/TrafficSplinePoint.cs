using System;
using System.Collections.Generic;
using System.Numerics;

namespace AssettoServer.Server.Ai
{
    public class TrafficSplinePoint
    {
        public int Id { get; init; }
        public Vector3 Point { get; init; }
        
        public TrafficSplineJunction JunctionStart { get; set; }
        public TrafficSplinePoint JunctionEnd { get; set; }
        public TrafficSplinePoint Previous { get; set; }
        public TrafficSplinePoint Next { get; set; }
        public TrafficSplinePoint Left { get; set; }
        public TrafficSplinePoint Right { get; set; }

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

        public List<TrafficSplinePoint> GetLanes()
        {
            var ret = new List<TrafficSplinePoint>();

            TrafficSplinePoint point = this;
            while (point.Left != null)
            {
                point = point.Left;
            }

            while (point != null)
            {
                ret.Add(point);
                point = point.Right;
            }
            
            return ret;
        }

        public TrafficSplinePoint RandomLane(Random random)
        {
            var lanes = GetLanes();
            return lanes[random.Next(lanes.Count)];
        }

        public float GetBankAngle(float lerp = 0)
        {
            Vector3 banking = Vector3.UnitZ;
            if (Left != null)
            {
                banking = Point - Left.Point;
            }
            else if (Right != null)
            {
                banking = Right.Point - Point;
            }
            
            float bankAngle = (float)(Math.Atan2(new Vector2(banking.Z, banking.X).Length(), banking.Y) - Math.PI / 2) * -1f;

            if (lerp != 0 && Next != null)
            {
                float nextAngle = Next.GetBankAngle();
                bankAngle = (float)MathUtils.Lerp(bankAngle, nextAngle, lerp);
            }

            return bankAngle;
        }
    }
}