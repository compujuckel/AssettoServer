using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Supercluster.KDTree;

namespace AssettoServer.Server.Ai
{
    public class TrafficMap
    {
        public Dictionary<string, TrafficSpline> Splines { get; }
        public Dictionary<int, TrafficSplinePoint> PointsById { get; }
        public KDTree<TrafficSplinePoint> KdTree { get; }
        public float MinCorneringSpeed { get; }
        
        public TrafficMap(string sourcePath, Dictionary<string, TrafficSpline> splines, float laneWidth)
        {
            Splines = splines;
            PointsById = new Dictionary<int, TrafficSplinePoint>();
            MinCorneringSpeed = Splines.Values.Min(s => s.MinCorneringSpeed);

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
            TrafficConfigurationParser.Parse(this, Path.Join(sourcePath, "config.yml"));
        }

        private Vector3[] CreateTreeData()
        {
            var data = new List<Vector3>();
            foreach (var point in PointsById)
            {
                data.Add(point.Value.Point);
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
            var nearest = KdTree.NearestNeighbors(position, 1)[0].Item2;
            float dist = Vector3.DistanceSquared(position, nearest.Point);

            return (nearest, dist);
        }
    }
}