using System;
using System.Collections.Generic;
using System.Numerics;
using AssettoServer.Utils;
using CsvHelper.Configuration.Attributes;
using Serilog;

namespace AssettoServer.Server.Ai;

public class TrafficSplinePoint
{
    public int Id { get; init; }
    public Vector3 Point { get; init; }
    
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
        
    [Ignore] public TrafficSplineJunction? JunctionStart { get; set; }
    [Ignore] public TrafficSplineJunction? JunctionEnd { get; set; }
    [Ignore] public TrafficSplinePoint? Previous { get; set; }
    [Ignore] public TrafficSplinePoint? Next { get; set; }
    [Ignore] public TrafficSplinePoint? Left { get; set; }
    [Ignore] public TrafficSplinePoint? Right { get; set; }

    public List<TrafficSplinePoint> GetLanes(bool twoWayTraffic = false)
    {
        var ret = new List<TrafficSplinePoint>();
        const int maxCount = 10;

        TrafficSplinePoint? point = Left;
        while (point != null && ret.Count < maxCount)
        {
            if (IsSameDirection(point))
            {
                ret.Add(point);
                point = point.Left;
            }
            else if (twoWayTraffic)
            {
                ret.Add(point);
                point = point.Right;
            }
            else
            {
                break;
            }
        }
            
        ret.Reverse();
        ret.Add(this);

        point = Right;
        while (point != null && ret.Count < maxCount)
        {
            if (IsSameDirection(point))
            {
                ret.Add(point);
                point = point.Right;
            }
            else if (twoWayTraffic)
            {
                ret.Add(point);
                point = point.Left;
            }
            else
            {
                break;
            }
        }

        if (ret.Count >= maxCount)
        {
            Log.Debug("Possible loop at AI spline point {SplinePointId}", Id);
        }

        return ret;
    }

    public TrafficSplinePoint RandomLane(bool twoWayTraffic = false)
    {
        var lanes = GetLanes(twoWayTraffic);
        return lanes[Random.Shared.Next(lanes.Count)];
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
        
    public Vector3 GetForwardVector()
    {
        if (Next != null)
        {
            return Next.Point - Point;
        }

        return Vector3.Zero;
    }

    public bool IsSameDirection(TrafficSplinePoint point)
    {
        return Vector3.Dot(GetForwardVector(), point.GetForwardVector()) > 0;
    }
}
