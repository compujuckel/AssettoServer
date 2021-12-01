using System;
using System.Collections.Generic;
using System.IO;
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
            List<TrafficSpline> splines = new List<TrafficSpline>();

            int idOffset = 0;
            // List of files should be ordered to guarantee consistent IDs for junctions etc.
            foreach (var file in Directory.EnumerateFiles(folder, "fast_lane*.ai").OrderBy(f => f))
            {
                var spline = FromFile(file, idOffset);
                splines.Add(spline);

                Log.Debug("Parsed {0}, id range {1} - {2} minSpeed {3}", file, idOffset, idOffset + spline.Points.Length - 1, spline.MinCorneringSpeed);
                idOffset += spline.Points.Length;
            }

            return new TrafficMap(Path.Join(folder, "fast_lane.ai"), splines, _server.Configuration.Extra.AiParams.LaneWidth);
        }

        public TrafficSpline FromFile(string filename, int idOffset = 0)
        {
            Log.Debug("Loading AI spline {0}", filename);
            using var reader = new BinaryReader(File.OpenRead(filename));

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
                points[i].Speed = reader.ReadSingle();
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

                points[i].MaxCorneringSpeed = PhysicsUtils.CalculateMaxCorneringSpeed(points[i].Radius) * _server.Configuration.Extra.AiParams.CorneringSpeedFactor;
                
                minCorneringSpeed = Math.Min(minCorneringSpeed, points[i].MaxCorneringSpeed);
            }

            for (var i = 0; i < detailCount; i++)
            {
                points[i].Previous = points[i == 0 ? detailCount - 1 : i - 1];
                points[i].Next = points[i == detailCount - 1 ? 0 : i + 1];

                points[i].Length = Vector3.Distance(points[i].Point, points[i].Next.Point);
            }

            /*using (var writer = new StreamWriter(Path.GetFileName(filename) + ".csv"))
            using (var csv = new CsvWriter(writer, new CultureInfo("de-DE", false)))
            {
                csv.WriteRecords(points);
            }*/

            return new TrafficSpline
            {
                Name = filename,
                Points = points,
                MinCorneringSpeed = minCorneringSpeed
            };
        }
    }
}