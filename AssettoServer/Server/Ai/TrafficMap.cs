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
        
        public KDTree<float, TrafficSplinePoint> KdTree { get; }

        public TrafficMap(string sourcePath, List<TrafficSpline> splines, float laneWidth)
        {
            SourcePath = sourcePath;
            Splines = splines;
            PointsById = new Dictionary<int, TrafficSplinePoint>();

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

            KdTree = new KDTree<float, TrafficSplinePoint>(3, treeData, treeNodes, (x, y) =>
            {
                double dist = 0;
                for (int i = 0; i < x.Length; i++)
                {
                    dist += (x[i] - y[i]) * (x[i] - y[i]);
                }

                return dist;
            });
            
            AdjacentLaneDetector.GetAdjacentLanesForMap(this, SourcePath + ".lanes", laneWidth);
            JunctionParser.Parse(this, SourcePath + ".junctions.csv");
        }

        public TrafficMapView NewView()
        {
            return new TrafficMapView(this);
        }

        private float[][] CreateTreeData()
        {
            var data = new List<float[]>();
            foreach (var point in PointsById)
            {
                var pointArray = new float[3];
                point.Value.Point.CopyTo(pointArray);
                
                data.Add(pointArray);
            }

            return data.ToArray();
        }

        public (TrafficSplinePoint point, float distanceSquared) WorldToSpline(Vector3 position)
        {
            var pointArray = new float[3];
            position.CopyTo(pointArray);

            var nearest = KdTree.NearestNeighbors(pointArray, 1)[0].Item2;
            float dist = Vector3.DistanceSquared(position, nearest.Point);

            return (nearest, dist);
        }
    }
}