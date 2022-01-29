using System;
using System.Collections.Generic;

namespace AssettoServer.Server.Ai
{
    public class TrafficSplineJunction
    {
        public TrafficSplinePoint? StartPoint { get; set; }
        public TrafficSplinePoint? EndPoint { get; set; }
        public float Probability { get; set; }
    }
}