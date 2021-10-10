using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Serilog;

namespace AssettoServer.Server.Ai
{
    public static class FastLaneParser
    {
        public static TrafficMap FromFile(string filename)
        {
            Log.Debug("Loading AI spline {0}", filename);
            using var reader = new BinaryReader(File.OpenRead(filename));

            reader.ReadInt32();
            int detailCount = reader.ReadInt32();
            reader.ReadInt32();
            reader.ReadInt32();

            TrafficSplinePoint[] points = new TrafficSplinePoint[detailCount];

            for (var i = 0; i < detailCount; i++)
            {
                var p = new TrafficSplinePoint
                {
                    Id = i,
                    Point = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle())
                };

                reader.ReadSingle();
                reader.ReadInt32();
                
                points[i] = p;
            }
            
            for(var i = 0; i < detailCount; i++)
            {
                points[i].Previous = points[i == 0 ? detailCount - 1 : i - 1];
                points[i].Next = points[i == detailCount - 1 ? 0 : i + 1];
            }

            List<TrafficSpline> splines = new List<TrafficSpline>()
            {
                new TrafficSpline("fast_lane.ai", points)
            };

            return new TrafficMap(filename, splines);
        }
    }
}