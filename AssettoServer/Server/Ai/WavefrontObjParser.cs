using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Serilog;

namespace AssettoServer.Server.Ai
{
    public static class WavefrontObjParser
    {
        public static TrafficMap ParseFile(string filename, float laneWidth)
        {
            Log.Debug("Loading traffic map {0}", filename);
            
            var lines = File.ReadLines(filename);

            List<TrafficSpline> splines = new List<TrafficSpline>();

            float heightOffset = 0;
            // obj-Files are one-indexed
            int objectStartIndex = 1;
            int id = 1;
            string currentSplineName = null;
            List<TrafficSplinePoint> points = null;
            foreach (string line in lines)
            {
                string[] words = line.Split();
                switch (words[0])
                {
                    case "#@heightoffset":
                        heightOffset = float.Parse(words[1]);
                        break;
                    case "o":
                        if (points != null)
                        {
                            objectStartIndex = objectStartIndex + points.Count;
                            var spline = new TrafficSpline(currentSplineName, points.ToArray());
                            splines.Add(spline);
                            Log.Debug("Spline {0} finished with {1} points", spline.Name, spline.Points.Length);
                        }
                        
                        currentSplineName = words[1];
                        points = new List<TrafficSplinePoint>();
                        Log.Debug("Found new spline {0}", currentSplineName);
                        break;
                    case "v":
                        if (points == null)
                        {
                            Log.Warning("Found vertex without object");
                            break;
                        }
                        float x = float.Parse(words[1]);
                        float y = float.Parse(words[2]) + heightOffset;
                        float z = float.Parse(words[3]);
                        points.Add(new TrafficSplinePoint
                        {
                            
                            Id = id++,
                            Point = new Vector3(x, y, z)
                        });
                        break;
                    case "l":
                        int start = int.Parse(words[1]) - objectStartIndex;
                        int end = int.Parse(words[2]) - objectStartIndex;

                        points[start].Next = points[end];
                        points[end].Previous = points[start];
                        break;
                }
            }

            if (points != null)
            {
                var spline = new TrafficSpline(currentSplineName, points.ToArray());
                splines.Add(spline);
                Log.Debug("Spline {0} finished with {1} points", spline.Name, spline.Points.Length);
            }

            return new TrafficMap(filename, splines, laneWidth);
        }
    }
}