using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using Serilog;

namespace AssettoServer.Server.Ai
{
    public class FastLaneParser
    {
        private readonly ACServer _server;

        public FastLaneParser(ACServer server)
        {
            _server = server;
        }

        public TrafficMap FromFiles(string folder)
        {
            Dictionary<string, TrafficSpline> splines = new();

            int idOffset = 0;
            // List of files should be ordered to guarantee consistent IDs for junctions etc.
            foreach (var file in Directory.EnumerateFiles(folder, "fast_lane*.ai?").OrderBy(f => f))
            {
                string filename = Path.GetFileName(file);
                
                TrafficSpline spline;
                if (file.EndsWith(".aiz"))
                {
                    using var fileStream = File.OpenRead(file);
                    using var compressed = new GZipStream(fileStream, CompressionMode.Decompress);
                    spline = FromFile(compressed, filename, idOffset);
                }
                else if(file.EndsWith(".ai"))
                {
                    using var fileStream = File.OpenRead(file);
                    spline = FromFile(fileStream, filename, idOffset);
                }
                else
                {
                    continue;
                }
                
                splines.Add(filename, spline);

                Log.Information("Parsed {Path}, id range {MinId} - {MaxId}, min. speed {MinSpeed} km/h", file, idOffset, idOffset + spline.Points.Length - 1, MathF.Round(spline.MinCorneringSpeed * 3.6f));
                idOffset += spline.Points.Length;
            }

            if (splines.Count == 0) 
                throw new InvalidOperationException($"No AI splines found. Please put at least one AI spline (fast_lane.ai) into {Path.GetFullPath(folder)}");

            return new TrafficMap(folder, splines, _server.Configuration.Extra.AiParams.LaneWidthMeters);
        }

        private TrafficSpline FromFile(Stream file, string name, int idOffset = 0)
        {
            Log.Debug("Loading AI spline {Path}", name);
            using var reader = new BinaryReader(file);

            float minCorneringSpeed = float.MaxValue;

            reader.ReadInt32(); // Version
            int detailCount = reader.ReadInt32();
            reader.ReadInt32(); // LapTime
            reader.ReadInt32(); // SampleCount

            TrafficSplinePoint[] points = new TrafficSplinePoint[detailCount];

            for (var i = 0; i < detailCount; i++)
            {
                var p = new TrafficSplinePoint
                {
                    Id = idOffset + i,
                    Point = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle())
                };

                reader.ReadSingle(); // Length
                reader.ReadInt32(); // ID

                points[i] = p;
            }

            int extraCount = reader.ReadInt32();
            if (extraCount != detailCount)
            {
                throw new ArgumentException("Count of spline points does not match extra spline points");
            }

            for (var i = 0; i < detailCount; i++)
            {
                /*points[i].Speed*/ _ = reader.ReadSingle();
                /*points[i].Gas*/ _ = reader.ReadSingle();
                /*points[i].Brake*/ _ = reader.ReadSingle();
                /*points[i].ObsoleteLatG*/ _ = reader.ReadSingle();
                points[i].Radius = reader.ReadSingle();
                /*points[i].SideLeft*/ _ = reader.ReadSingle();
                /*points[i].SideRight*/ _ = reader.ReadSingle();
                points[i].Camber = reader.ReadSingle() /* camber */ * reader.ReadSingle() /* direction, either 1 or -1 */;
                /*points[i].Normal*/ _ = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                points[i].Length = reader.ReadSingle();
                /*points[i].ForwardVector*/ _ = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                /*points[i].Tag*/ _ = reader.ReadSingle();
                /*points[i].Grade*/ _ = reader.ReadSingle();

                // For point-to-point splines the last point might be completely off
                if (i == detailCount - 1 && points[i].Radius < 1)
                {
                    Log.Debug("Resetting radius of last spline point");
                    points[i].Radius = 1000;
                }
                
                points[i].MaxCorneringSpeed = PhysicsUtils.CalculateMaxCorneringSpeed(points[i].Radius) * _server.Configuration.Extra.AiParams.CorneringSpeedFactor;
                
                minCorneringSpeed = Math.Min(minCorneringSpeed, points[i].MaxCorneringSpeed);
            }

            for (var i = 0; i < detailCount; i++)
            {
                points[i].Previous = points[i == 0 ? detailCount - 1 : i - 1];
                points[i].Next = points[i == detailCount - 1 ? 0 : i + 1];

                points[i].Length = Vector3.Distance(points[i].Point, points[i].Next!.Point);
            }

            bool closedLoop = Vector3.Distance(points[0].Point, points[^1].Point) < 50;
            if (!closedLoop)
            {
                points[0].Previous = null;
                points[^1].Next = null;
                points[^1].Length = 1;
                Log.Debug("Distance between spline start and end too big, not closing loop");
            }

            /*using (var writer = new StreamWriter(Path.GetFileName(filename) + ".csv"))
            using (var csv = new CsvWriter(writer, new CultureInfo("de-DE", false)))
            {
                csv.WriteRecords(points);
            }*/

            return new TrafficSpline
            {
                Name = name,
                Points = points,
                MinCorneringSpeed = minCorneringSpeed
            };
        }
    }
}