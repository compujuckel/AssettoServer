using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AssettoServer.Network.Packets.Outgoing;
using AssettoServer.Server.Ai.Structs;
using Serilog;
using Supercluster.KDTree;

namespace AssettoServer.Server.Ai;

public class AiPackage
{
    public Dictionary<string, TrafficSpline> Splines { get; }
    public SplinePointStruct[] PointsById;
    public List<SplineJunctionStruct> Junctions { get; } = new();
    public KDTree<int> KdTree { get; }
    public float MinRadius { get; }

    private readonly ILogger _logger;

    public AiPackage(Dictionary<string, TrafficSpline> splines, float laneWidth, bool twoWayTraffic = false, TrafficConfiguration? configuration = null, ILogger? logger = null)
    {
        _logger = logger ?? Log.Logger;
        Splines = splines;
        MinRadius = Splines.Values.Min(s => s.MinRadius);

        int total = splines.Values.Sum(s => s.Points.Length);
        PointsById = new SplinePointStruct[total];
        
        int i = 0;
        foreach (var point in splines.Values.SelectMany(spline => spline.Points))
        {
            if (i != point.Id)
            {
                throw new InvalidOperationException("Mismatched ID");
            }

            PointsById[i] = point;
            i++;
        }
            
        var treeData = CreateTreeData();
        var treeNodes = Enumerable.Range(0, PointsById.Length).ToArray();

        KdTree = new KDTree<int>(treeData, treeNodes);
            
        AdjacentLaneDetector.DetectAdjacentLanes(this, laneWidth, twoWayTraffic);
        if (configuration != null)
        {
            ApplyConfiguration(configuration);
        }
    }

    private Vector3[] CreateTreeData()
    {
        var data = new List<Vector3>();
        foreach (var point in PointsById)
        {
            data.Add(point.Position);
        }

        return data.ToArray();
    }

    public ref SplinePointStruct GetByIdentifier(string identifier)
    {
        int separator = identifier.IndexOf('@');
        string splineName = identifier.Substring(0, separator);
        int id = int.Parse(identifier.Substring(separator + 1));
        int globalId = Splines[splineName].Points[id].Id;
        return ref PointsById[globalId];
    }

    public (int PointId, float DistanceSquared) WorldToSpline(Vector3 position)
    {
        var nearest = KdTree.NearestNeighbors(position, 1);
        if (nearest.Length == 0)
        {
            return (-1, float.PositiveInfinity);
        }

        float dist = Vector3.DistanceSquared(position, PointsById[nearest[0].Item2].Position);
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
                ref var startPoint = ref PointsById[startSpline.Points[^1].Id];
                
                startPoint.NextId = endPoint.Id;
                
                var jct = new SplineJunctionStruct
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

                ref var startPoint = ref PointsById[startSpline.Points[junction.Start].Id];
                ref var endPoint = ref GetByIdentifier(junction.End);

                var jct = new SplineJunctionStruct
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
