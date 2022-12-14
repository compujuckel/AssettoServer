using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AssettoServer.Network.Packets.Outgoing;
using AssettoServer.Server.Ai.Configuration;
using AssettoServer.Server.Ai.Structs;
using Serilog;
using Supercluster.KDTree;

namespace AssettoServer.Server.Ai;

public class MutableAiSpline
{
    public Dictionary<string, FastLane> Splines { get; }
    public SplinePoint[] Points { get; }
    public List<SplineJunction> Junctions { get; } = new();
    public KDTree<int> KdTree { get; }
    public List<int[]> Lanes { get; }

    private readonly ILogger _logger;

    internal MutableAiSpline(Dictionary<string, FastLane> splines, float laneWidth, bool twoWayTraffic = false, TrafficConfiguration? configuration = null, ILogger? logger = null)
    {
        _logger = logger ?? Log.Logger;
        Splines = splines;

        int total = splines.Values.Sum(s => s.Points.Length);
        Points = new SplinePoint[total];
        
        int i = 0;
        foreach (var point in splines.Values.SelectMany(spline => spline.Points))
        {
            if (i != point.Id)
            {
                throw new InvalidOperationException("Mismatched ID");
            }

            Points[i] = point;
            i++;
        }
            
        var treeData = CreateTreeData();
        var treeNodes = Enumerable.Range(0, Points.Length).ToArray();

        KdTree = new KDTree<int>(treeData, treeNodes);
            
        AdjacentLaneDetector.DetectAdjacentLanes(this, laneWidth, twoWayTraffic);

        var ops = new SplinePointOperations(Points);
        Lanes = new List<int[]>();
        int offset = 0;
        for (i = 0; i < Points.Length; i++)
        {
            if (Points[i].LanesId == -1)
            {
                var lanes = ops.GetLanes(i, twoWayTraffic).ToArray();
                Lanes.Add(lanes);
                foreach (var lane in lanes)
                {
                    Points[lane].LanesId = offset;
                }

                offset += sizeof(int) + lanes.Length * sizeof(int);
            }
        }
        
        if (configuration != null)
        {
            ApplyConfiguration(configuration);
        }
    }

    private Vector3[] CreateTreeData()
    {
        var data = new List<Vector3>();
        foreach (var point in Points)
        {
            data.Add(point.Position);
        }

        return data.ToArray();
    }

    private ref SplinePoint GetByIdentifier(string identifier)
    {
        int separator = identifier.IndexOf('@');
        string splineName = identifier.Substring(0, separator);
        int id = int.Parse(identifier.Substring(separator + 1));
        int globalId = Splines[splineName].Points[id].Id;
        return ref Points[globalId];
    }

    public (int PointId, float DistanceSquared) WorldToSpline(Vector3 position)
    {
        var nearest = KdTree.NearestNeighbors(position, 1);
        if (nearest.Length == 0)
        {
            return (-1, float.PositiveInfinity);
        }

        float dist = Vector3.DistanceSquared(position, Points[nearest[0].Item2].Position);
        return (nearest[0].Item2, dist);
    }

    private void ApplyConfiguration(TrafficConfiguration config)
    {
        int junctionsIndex = 0;
        
        foreach (var spline in config.Splines)
        {
            var startSpline = Splines[spline.Name];

            if (spline.ConnectEnd != null)
            {
                ref var endPoint = ref GetByIdentifier(spline.ConnectEnd);
                ref var startPoint = ref Points[startSpline.Points[^1].Id];
                
                startPoint.NextId = endPoint.Id;
                
                var jct = new SplineJunction
                {
                    Id = junctionsIndex++,
                    StartPointId = startPoint.Id,
                    EndPointId = endPoint.Id,
                    Probability = 1.0f,
                    IndicateWhenTaken = IndicatorToStatusFlags(spline.IndicateEnd),
                    IndicateDistancePre = spline.IndicateEndDistancePre,
                    IndicateDistancePost = spline.IndicateEndDistancePost
                };

                startPoint.JunctionStartId = jct.Id;
                endPoint.JunctionEndId = jct.Id;

                Junctions.Add(jct);
            }

            foreach (var junction in spline.Junctions)
            {
                _logger.Debug("Junction {Name} from {StartSpline} {StartId} to {End}", junction.Name, startSpline.Name, junction.Start, junction.End);

                ref var startPoint = ref Points[startSpline.Points[junction.Start].Id];
                ref var endPoint = ref GetByIdentifier(junction.End);

                var jct = new SplineJunction
                {
                    Id = junctionsIndex++,
                    StartPointId = startPoint.Id,
                    EndPointId = endPoint.Id,
                    Probability = junction.Probability,
                    IndicateWhenTaken = IndicatorToStatusFlags(junction.IndicateWhenTaken),
                    IndicateWhenNotTaken = IndicatorToStatusFlags(junction.IndicateWhenNotTaken),
                    IndicateDistancePre = junction.IndicateDistancePre,
                    IndicateDistancePost = junction.IndicateDistancePost
                };

                startPoint.JunctionStartId = jct.Id;
                endPoint.JunctionEndId = jct.Id;

                Junctions.Add(jct);
            }
        }
    }

    private static CarStatusFlags IndicatorToStatusFlags(Indicator indicator)
    {
        return indicator switch
        {
            Indicator.Left => CarStatusFlags.IndicateLeft,
            Indicator.Right => CarStatusFlags.IndicateRight,
            _ => 0
        };
    }
}
