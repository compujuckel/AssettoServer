using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using CsvHelper;
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

        public TrafficMap FromFile(string filename)
        {
            Log.Debug("Loading AI spline {0}", filename);
            using var reader = new BinaryReader(File.OpenRead(filename));

            reader.ReadInt32(); // Version
            int detailCount = reader.ReadInt32();
            reader.ReadInt32(); // LapTime
            reader.ReadInt32(); // SampleCount

            TrafficSplinePoint[] points = new TrafficSplinePoint[detailCount];

            for (var i = 0; i < detailCount; i++)
            {
                var p = new TrafficSplinePoint
                {
                    Id = i,
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

                points[i].MaxCorneringSpeed = PhysicsUtils.CalculateMaxCorneringSpeed(points[i].Radius);
                points[i].TargetSpeed = points[i].MaxCorneringSpeed;
            }

            for (var i = 0; i < detailCount; i++)
            {
                points[i].Previous = points[i == 0 ? detailCount - 1 : i - 1];
                points[i].Next = points[i == detailCount - 1 ? 0 : i + 1];
            }

            foreach (var point in points)
            {
                if (point.MaxCorneringSpeed < _server.Configuration.Extra.AiParams.MaxSpeedMs)
                {
                    float brakingDistance = PhysicsUtils.CalculateBrakingDistance(_server.Configuration.Extra.AiParams.MaxSpeedMs - point.MaxCorneringSpeed, -_server.Configuration.Extra.AiParams.DefaultDeceleration);

                    point.BrakingDistance = brakingDistance;

                    float distanceTraveled = 0;
                    var currentPoint = point;
                    while (distanceTraveled < brakingDistance)
                    {
                        currentPoint.TargetSpeed = Math.Min(currentPoint.TargetSpeed, point.MaxCorneringSpeed);

                        if (currentPoint.MaxCorneringSpeed > _server.Configuration.Extra.AiParams.MaxSpeedMs)
                        {
                            distanceTraveled += currentPoint.Previous.Length;
                        }

                        currentPoint = currentPoint.Previous;
                    }
                }
            }

            List<TrafficSpline> splines = new List<TrafficSpline>()
            {
                new TrafficSpline("fast_lane.ai", points)
            };
            
            /*using (var writer = new StreamWriter("fast_lane.csv"))
            using (var csv = new CsvWriter(writer, new CultureInfo("de-DE", false)))
            {
                csv.WriteRecords(points);
            }*/

            return new TrafficMap(filename, splines);
        }
    }
}