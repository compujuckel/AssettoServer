using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AssettoServer.Network.Packets.Outgoing;
using Serilog;
using Supercluster.KDTree;

namespace AssettoServer.Server.Ai
{
    public class TrafficMap
    {
        public Dictionary<string, TrafficSpline> Splines { get; }
        public Dictionary<int, TrafficSplinePoint> PointsById { get; }
        public KDTree<TrafficSplinePoint> KdTree { get; }
        public float MinRadius { get; }

        private readonly ILogger _logger;

        public TrafficMap(Dictionary<string, TrafficSpline> splines, float laneWidth, TrafficConfiguration? configuration = null, ILogger? logger = null)
        {
            _logger = logger ?? Log.Logger;
            Splines = splines;
            PointsById = new Dictionary<int, TrafficSplinePoint>();
            MinRadius = Splines.Values.Min(s => s.MinRadius);

            foreach (var point in splines.Values.SelectMany(spline => spline.Points))
            {
                if (PointsById.ContainsKey(point.Id))
                {
                    throw new InvalidOperationException("Traffic map has spline points with duplicate id");
                }
                
                PointsById.Add(point.Id, point);
            }
            
            var treeData = CreateTreeData();
            var treeNodes = PointsById.Values.ToArray();

            KdTree = new KDTree<TrafficSplinePoint>(treeData, treeNodes);
            
            AdjacentLaneDetector.DetectAdjacentLanes(this, laneWidth);
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
                data.Add(point.Value.Position);
            }

            return data.ToArray();
        }

        public TrafficSplinePoint GetByIdentifier(string identifier)
        {
            int separator = identifier.IndexOf('@');
            string splineName = identifier.Substring(0, separator);
            int id = int.Parse(identifier.Substring(separator + 1));
            return Splines[splineName].Points[id];
        }

        public (TrafficSplinePoint point, float distanceSquared) WorldToSpline(Vector3 position)
        {
            var nearest = KdTree.NearestNeighbors(position, 1);
            if (nearest.Length == 0)
            {
                throw new ArgumentException($"No nearest point found for input vector {position}");
            }
            
            float dist = Vector3.DistanceSquared(position, nearest[0].Item2.Position);

            return (nearest[0].Item2, dist);
        }

        private void ApplyConfiguration(TrafficConfiguration config)
        {
            foreach (var spline in config.Splines)
            {
                var startSpline = Splines[spline.Name];

                if (spline.ConnectEnd != null)
                {
                    var endPoint = GetByIdentifier(spline.ConnectEnd);
                    var startPoint = startSpline.Points[^1];
                    startPoint.Next = endPoint;
                
                    var jct = new TrafficSplineJunction
                    {
                        StartPoint = startPoint,
                        EndPoint = endPoint,
                        Probability = 1.0f,
                        IndicateWhenTaken = IndicatorToStatusFlags(spline.IndicateEnd),
                        IndicateDistancePre = spline.IndicateEndDistancePre,
                        IndicateDistancePost = spline.IndicateEndDistancePost
                    };

                    startPoint.JunctionStart = jct;
                    endPoint.JunctionEnd = jct;
                }

                foreach (var junction in spline.Junctions)
                {
                    _logger.Debug("Junction {Name} from {StartSpline} {StartId} to {End}", junction.Name, startSpline.Name, junction.Start, junction.End);

                    var startPoint = startSpline.Points[junction.Start];
                    var endPoint = GetByIdentifier(junction.End);

                    var jct = new TrafficSplineJunction
                    {
                        StartPoint = startPoint,
                        EndPoint = endPoint,
                        Probability = junction.Probability,
                        IndicateWhenTaken = IndicatorToStatusFlags(junction.IndicateWhenTaken),
                        IndicateWhenNotTaken = IndicatorToStatusFlags(junction.IndicateWhenNotTaken),
                        IndicateDistancePre = junction.IndicateDistancePre,
                        IndicateDistancePost = junction.IndicateDistancePost
                    };

                    startPoint.JunctionStart = jct;
                    endPoint.JunctionEnd = jct;
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
}
