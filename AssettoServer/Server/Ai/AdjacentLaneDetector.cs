using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using SerilogTimings;

namespace AssettoServer.Server.Ai
{
    public static class AdjacentLaneDetector
    {
        private const float LaneWidth = 3.27f; // TODO configurable

        public static void GetAdjacentLanesForMap(TrafficMap map, string cacheFilePath)
        {
            if (File.Exists(cacheFilePath))
            {
                ParseCache(map, cacheFilePath);
            }
            else
            {
                DetectAdjacentLanes(map);
                WriteCache(map, cacheFilePath);
            }
        }

        private static void ParseCache(TrafficMap map, string cacheFilePath)
        {
            Log.Information("Parsing existing lane cache...");
            
            using var t = Operation.Time("Parsing existing lane cache");
            
            using (var reader = new BinaryReader(File.OpenRead(cacheFilePath)))
            {
                long fileLength = reader.BaseStream.Length;
                int pointCount = reader.ReadInt32();
                if (pointCount != map.Splines.Select(spline => spline.Points.Length).Sum())
                {
                    Log.Error("Point count of lane cache differs from point count of traffic map. Cache disabled");
                    return;
                }

                var allPoints = map.Splines.SelectMany(spline => spline.Points).ToList();

                try
                {
                    while (true)
                    {
                        int idLeft = reader.ReadInt32();
                        int idRight = reader.ReadInt32();

                        var pointLeft = map.PointsById[idLeft];
                        var pointRight = map.PointsById[idRight];

                        pointLeft.Right = pointRight;
                        pointRight.Left = pointLeft;
                    }
                }
                catch (EndOfStreamException)
                {
                    
                }
            }
        }

        private static void WriteCache(TrafficMap map, string cacheFilePath)
        {
            Log.Information("Writing lane cache to file");
            
            using (var writer = new BinaryWriter(File.OpenWrite(cacheFilePath)))
            {
                writer.Write(map.Splines.Select(spline => spline.Points.Length).Sum());
                foreach (var spline in map.Splines)
                {
                    foreach (var point in spline.Points)
                    {
                        if (point.Right != null)
                        {
                            writer.Write(point.Id);
                            writer.Write(point.Right.Id);
                        }
                    }
                }
            }
        }

        private static Vector3 OffsetVec(Vector3 pos, float angle, float offset)
        {
            return new()
            {
                X = (float) (pos.X + Math.Cos(angle * Math.PI / 180) * offset),
                Y = pos.Y,
                Z = (float) (pos.Z - Math.Sin(angle * Math.PI / 180) * offset)
            };
        }

        private static void DetectAdjacentLanes(TrafficMap map)
        {
            Log.Information("Adjacent lane detection...");
            
            using var t = Operation.Time("Adjacent lane detection");

            int i = 0;
            
            foreach (var spline in map.Splines)
            {
                Parallel.ForEach(spline.Points, point =>
                {
                    if (point.Right == null && point.Next != null)
                    {
                        float direction = (float) (Math.Atan2(point.Point.Z - point.Next.Point.Z, point.Next.Point.X - point.Point.X) * (180 / Math.PI) * -1);

                        var leftVec = OffsetVec(point.Point, -direction + 90, LaneWidth);

                        var found = map.WorldToSpline(leftVec);

                        if (found.distanceSquared < 2 * 2)
                        {
                            // TODO make sure lanes are facing in the same direction.
                            // This probably breaks on right hand drive tracks
                            
                            point.Left = found.point;
                            found.point.Right = point;
                        }
                    }

                    Interlocked.Increment(ref i);

                    if (i % 1000 == 0)
                    {
                        Log.Debug("Detecting adjacent lanes, progress {0}/{1} points, {2}%", i, spline.Points.Length, Math.Round((double)i / spline.Points.Length * 100.0));
                    }
                });
            }
        }
    }
}