using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using AssettoServer.Server.Ai.Configuration;
using AssettoServer.Server.Ai.Structs;
using AssettoServer.Server.Configuration;
using AssettoServer.Utils;
using Serilog;
using YamlDotNet.Serialization;

namespace AssettoServer.Server.Ai;

public class FastLaneParser
{
    private readonly ACServerConfiguration _configuration;

    private ILogger _logger = Log.Logger;

    public FastLaneParser(ACServerConfiguration configuration)
    {
        _configuration = configuration;
    }

    private void CheckConfig(TrafficConfiguration configuration)
    {
        if (!string.IsNullOrWhiteSpace(configuration.Author))
        {
            Log.Information("Loading AI spline by {Author}, version {Version}", configuration.Author, configuration.Version);
        }
            
        if (!string.IsNullOrWhiteSpace(configuration.Track) && Path.GetFileName(_configuration.Server.Track) != configuration.Track)
        {
            throw new InvalidOperationException($"Mismatched AI spline, AI spline is for track {configuration.Track}");
        }
    }

    public MutableAiSpline FromFiles(string folder)
    {
        Dictionary<string, FastLane> splines = new();
        TrafficConfiguration? configuration = null;

        int idOffset = 0;

        string aipPath = Path.Join(folder, "fast_lane.aip");
        if (File.Exists(aipPath))
        {
            Log.Information("Loading from AI package {Path}", aipPath);

            _logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Logger(Log.Logger)
                .CreateLogger();

            using var aipFile = ZipFile.OpenRead(aipPath);

            var configEntry = aipFile.GetEntry("config.yml");
            if (configEntry != null)
            {
                using var fileStream = configEntry.Open();
                using var reader = new StreamReader(fileStream);
                        
                var deserializer = new DeserializerBuilder().Build();
                configuration = deserializer.Deserialize<TrafficConfiguration>(reader);
                CheckConfig(configuration);
            }
                
            foreach (var entry in aipFile.Entries)
            {
                if (entry.Name.EndsWith(".ai"))
                {
                    using var fileStream = entry.Open();
                    var spline = FromFile(fileStream, entry.Name, idOffset);
                    splines.Add(entry.Name, spline);
                    
                    _logger.Debug("Parsed {Path}, id range {MinId} - {MaxId}", entry, idOffset, idOffset + spline.Points.Length - 1);
                    idOffset += spline.Points.Length;
                }
            }
        }
        else
        {
            string configPath = Path.Join(folder, "config.yml");
            if (File.Exists(configPath))
            {
                using var file = File.OpenText(configPath);
                var deserializer = new DeserializerBuilder().Build();
                configuration = deserializer.Deserialize<TrafficConfiguration>(file);
                CheckConfig(configuration);
            }

            if (!Directory.Exists(folder))
            {
                throw new ConfigurationException($"No ai folder found. Please put at least one AI spline fast_lane.ai(p) into {Path.GetFullPath(folder)}");
            }
             
            // List of files should be ordered to guarantee consistent IDs for junctions etc.
            foreach (string file in Directory.EnumerateFiles(folder, "fast_lane*.ai").OrderBy(f => f))
            {
                string filename = Path.GetFileName(file);

                using var fileStream = File.OpenRead(file);
                var spline = FromFile(fileStream, filename, idOffset);
                splines.Add(filename, spline);

                _logger.Debug("Parsed {Path}, id range {MinId} - {MaxId}", file, idOffset, idOffset + spline.Points.Length - 1);
                idOffset += spline.Points.Length;
            }
        }

        if (splines.Count == 0)
        {
            throw new InvalidOperationException($"No AI splines found. Please put at least one AI spline fast_lane.ai(p) into {Path.GetFullPath(folder)}");
        }

        return new MutableAiSpline(splines, _configuration.Extra.AiParams.LaneWidthMeters, _configuration.Extra.AiParams.TwoWayTraffic, configuration, _logger);
    }

    private SplinePoint[] FromFileV7(BinaryReader reader, int idOffset)
    {
        int detailCount = reader.ReadInt32();
        reader.ReadInt32(); // LapTime
        reader.ReadInt32(); // SampleCount

        var points = new SplinePoint[detailCount];

        for (var i = 0; i < detailCount; i++)
        {
            var p = new SplinePoint
            {
                Id = idOffset + i,
                Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                JunctionStartId = -1,
                JunctionEndId = -1,
                LeftId = -1,
                RightId = -1,
                NextId = -1,
                PreviousId = -1,
                LanesId = -1
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
        }

        return points;
    }
        
    private SplinePoint[] FromFileVn1(BinaryReader reader, int idOffset)
    {
        int detailCount = reader.ReadInt32();
        
        var points = new SplinePoint[detailCount];

        for (var i = 0; i < detailCount; i++)
        {
            points[i] = new SplinePoint
            {
                Id = idOffset + i,
                Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                Radius = reader.ReadSingle(),
                Camber = reader.ReadSingle(),
                JunctionStartId = -1,
                JunctionEndId = -1,
                LeftId = -1,
                RightId = -1,
                NextId = -1,
                PreviousId = -1,
                LanesId = -1
            };
        }

        return points;
    }

    private FastLane FromFile(Stream file, string name, int idOffset = 0)
    {
        _logger.Debug("Loading AI spline {Path}", name);
        using var reader = new BinaryReader(file);

        int version = reader.ReadInt32();
        var points = version switch
        {
            7 => FromFileV7(reader, idOffset),
            -1 => FromFileVn1(reader, idOffset),
            _ => throw new InvalidOperationException($"Unknown spline version {version}")
        };

        MovingAverage? avg = null;
        if (_configuration.Extra.AiParams.SmoothCamber)
        {
            avg = new MovingAverage(5);
        }

        for (var i = 0; i < points.Length; i++)
        {
            // For point-to-point splines the last point might be completely off
            if (i == points.Length - 1 && points[i].Radius < 1)
            {
                _logger.Debug("Resetting radius of last spline point");
                points[i].Radius = 1000;
            }

            points[i].PreviousId = points[i == 0 ? points.Length - 1 : i - 1].Id;
            ref var nextPoint = ref points[i == points.Length - 1 ? 0 : i + 1];
            points[i].NextId = nextPoint.Id;

            points[i].Length = Vector3.Distance(points[i].Position, nextPoint.Position);

            if (avg != null)
            {
                points[i].Camber = avg.Next(points[i].Camber);
            }
        }

        bool closedLoop = Vector3.Distance(points[0].Position, points[^1].Position) < 50;
        if (!closedLoop)
        {
            points[0].PreviousId = -1;
            points[^1].NextId = -1;
            points[^1].Length = 1;
            _logger.Debug("Distance between spline start and end too big, not closing loop");
        }

        return new FastLane
        {
            Name = name,
            Points = points,
        };
    }
}
