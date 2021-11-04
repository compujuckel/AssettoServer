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
            Log.Debug("extraCount={0}, detailCount={1}", extraCount, detailCount);

            for (var i = 0; i < detailCount; i++)
            {
                points[i].Speed = reader.ReadSingle();
                points[i].Gas = reader.ReadSingle();
                points[i].Brake = reader.ReadSingle();
                /*points[i].ObsoleteLatG*/ _ = reader.ReadSingle();
                points[i].Radius = reader.ReadSingle();
                /*points[i].SideLeft*/ _ = reader.ReadSingle();
                /*points[i].SideRight*/ _ = reader.ReadSingle();
                points[i].Camber = reader.ReadSingle();
                /*points[i].Direction*/ _ = reader.ReadSingle();
                points[i].Normal = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                points[i].Length = reader.ReadSingle();
                points[i].ForwardVector = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                /*points[i].Tag*/ _ = reader.ReadSingle();
                /*points[i].Grade*/ _ = reader.ReadSingle();

                points[i].MaxCorneringSpeed = CalculateMaxCorneringSpeed(points[i].Radius);
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
                    float brakingDistance = CalculateBrakingDistance(point.MaxCorneringSpeed);

                    point.BrakingDistance = brakingDistance;

                    float distanceTraveled = 0;
                    var currentPoint = point;
                    while (distanceTraveled < brakingDistance)
                    {
                        currentPoint.TargetSpeed = currentPoint.TargetSpeed > 0 ? Math.Min(currentPoint.TargetSpeed, point.MaxCorneringSpeed) : point.MaxCorneringSpeed;
                        
                        var prevPoint = currentPoint.Previous;
                        distanceTraveled += prevPoint.Length;
                        currentPoint = prevPoint;
                    }
                }
                else
                {
                    point.TargetSpeed = _server.Configuration.Extra.AiParams.MaxSpeedMs;
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

        private static float CalculateMaxCorneringSpeed(float radius)
        {
            const float staticFrictionCoefficient = 1;
            const float gravityAcceleration = 9.81f;
            const float vehicleMass = 1000;
            const float roadSlope = 0;

            float staticFriction = staticFrictionCoefficient * vehicleMass * gravityAcceleration * (float)Math.Sin(roadSlope);
            float netForce = staticFriction + (vehicleMass * gravityAcceleration * (float)Math.Cos(roadSlope));
            float speed = (float)Math.Sqrt(netForce * radius / vehicleMass);

            return speed * 0.8f;
        }

        private float CalculateBrakingDistance(float speed)
        {
            return (float) Math.Pow(_server.Configuration.Extra.AiParams.MaxSpeedMs - speed, 2) / (2 * -_server.Configuration.Extra.AiParams.DefaultDeceleration);
        }
    }
}