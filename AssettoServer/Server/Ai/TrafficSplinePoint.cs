using System;
using System.Collections.Generic;
using System.Numerics;
using CsvHelper.Configuration.Attributes;

namespace AssettoServer.Server.Ai
{
    public class TrafficSplinePoint
    {
        public int Id { get; init; }
        public Vector3 Point { get; init; }
        
        public float Speed { get; set; }
        public float MaxCorneringSpeed { get; set; }
        public float TargetSpeed { get; set; }
        public float BrakingDistance { get; set; }
        //public float Gas { get; set; }
        //public float Brake { get; set; }
        //public float ObsoleteLatG { get; set; }
        public float Radius { get; set; }
        //public float SideLeft { get; set; }
        //public float SideRight { get; set; }
        public float Camber { get; set; }
        //public float Direction { get; set; }
        //public Vector3 Normal { get; set; }
        public float Length { get; set; }
        //public Vector3 ForwardVector { get; set; }
        //public float Tag { get; set; }
        //public float Grade { get; set; }
        
        [Ignore] public TrafficSplineJunction JunctionStart { get; set; }
        [Ignore] public TrafficSplineJunction JunctionEnd { get; set; }
        [Ignore] public TrafficSplinePoint Previous { get; set; }
        [Ignore] public TrafficSplinePoint Next { get; set; }
        [Ignore] public TrafficSplinePoint Left { get; set; }
        [Ignore] public TrafficSplinePoint Right { get; set; }

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

            for (int i = 0; i > count; i--)
            {
                if (ret.Previous == null)
                {
                    return null;
                }

                ret = ret.Previous;
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

        public float GetCamber(float lerp = 0)
        {
            float camber = Camber;
            
            if (lerp != 0 && Next != null)
            {
                camber = (float)MathUtils.Lerp(camber, Next.GetCamber(), lerp);
            }

            return camber;
        }
    }
}