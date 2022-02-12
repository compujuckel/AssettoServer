using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Supercluster.KDTree;

namespace AssettoServer.Server.Ai
{
    public class TrafficMap
    {
        public string SourcePath { get; }
        public List<TrafficSpline> Splines { get; }
        public Dictionary<int, TrafficSplinePoint> PointsById { get; }
        public KDTree<TrafficSplinePoint> KdTree { get; }
        public float MinCorneringSpeed { get; }
        
        public TrafficMap(string sourcePath, List<TrafficSpline> splines, float laneWidth)
        {
            SourcePath = sourcePath;
            Splines = splines;
            PointsById = new Dictionary<int, TrafficSplinePoint>();
            MinCorneringSpeed = Splines.Min(s => s.MinCorneringSpeed);

            foreach (var point in splines.SelectMany(spline => spline.Points))
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
            JunctionParser.Parse(this, SourcePath + ".junctions.csv");
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

        public (TrafficSplinePoint point, float distanceSquared) WorldToSpline(Vector3 position)
        {
            var nearest = KdTree.NearestNeighbors(position, 1)[0].Item2;
            float dist = Vector3.DistanceSquared(position, nearest.Point);

            return (nearest, dist);
        }
    }
}