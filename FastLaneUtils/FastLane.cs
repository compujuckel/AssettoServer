using System.IO.Compression;
using System.Numerics;
using Serilog;

namespace FastLaneUtils;

public class FastLane
{
    public int Version { get; set; }
    public int DetailCount { get; set; }
    public int LapTime { get; set; }
    public int SampleCount { get; set; }
    public int ExtraCount { get; set; }
    public List<SplinePoint> Points { get; set; } = null!;

    private FastLane()
    {
        
    }

    public void Cut(int start, int end)
    {
        if (end > 0)
        {
            int count = Points.Count - end - 1;
            Log.Debug("Removing {Count} points from end of spline", count);
            Points.RemoveRange(end + 1, Points.Count - end - 1);
        }

        if (start > 0)
        {
            Log.Debug("Removing {Count} points from start of spline", start);
            Points.RemoveRange(0, start);
        }
    }

    public static FastLane FromFile(string path)
    {
        using var file = File.OpenRead(path);
        using var reader = new BinaryReader(file);

        var fastLane = new FastLane
        {
            Version = reader.ReadInt32()
        };

        return fastLane.Version switch
        {
            7 => FromFileV7(fastLane, reader),
            -1 => FromFileVn1(fastLane, reader),
            _ => throw new InvalidOperationException($"Unknown fast lane version {fastLane.Version}")
        };
    }

    private static FastLane FromFileV7(FastLane fastLane, BinaryReader reader)
    {
        fastLane.DetailCount = reader.ReadInt32();
        fastLane.LapTime = reader.ReadInt32();
        fastLane.SampleCount = reader.ReadInt32();

        fastLane.Points = new List<SplinePoint>(fastLane.DetailCount);
        
        for (var i = 0; i < fastLane.DetailCount; i++)
        {
            var p = new SplinePoint
            {
                Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                Length = reader.ReadSingle(),
                Id = reader.ReadInt32()
            };

            fastLane.Points.Add(p);
        }

        fastLane.ExtraCount = reader.ReadInt32();
        if (fastLane.ExtraCount != fastLane.DetailCount)
        {
            throw new ArgumentException("Count of spline points does not match extra spline points");
        }
        
        for (var i = 0; i < fastLane.DetailCount; i++)
        {
            fastLane.Points[i].Speed = reader.ReadSingle();
            fastLane.Points[i].Gas = reader.ReadSingle();
            fastLane.Points[i].Brake = reader.ReadSingle();
            fastLane.Points[i].ObsoleteLatG = reader.ReadSingle();
            fastLane.Points[i].Radius = reader.ReadSingle();
            fastLane.Points[i].SideLeft = reader.ReadSingle();
            fastLane.Points[i].SideRight = reader.ReadSingle();
            fastLane.Points[i].Camber = reader.ReadSingle();
            fastLane.Points[i].Direction = reader.ReadSingle();
            fastLane.Points[i].Normal = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            fastLane.Points[i].DetailLength = reader.ReadSingle();
            fastLane.Points[i].Forward = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            fastLane.Points[i].Tag = reader.ReadSingle();
            fastLane.Points[i].Grade = reader.ReadSingle();
        }

        return fastLane;
    }

    private static FastLane FromFileVn1(FastLane fastLane, BinaryReader reader)
    {
        fastLane.DetailCount = reader.ReadInt32();

        fastLane.Points = new List<SplinePoint>(fastLane.DetailCount);

        for (var i = 0; i < fastLane.DetailCount; i++)
        {
            var p = new SplinePoint
            {
                Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                Radius = reader.ReadSingle()
            };
            
            float camberEncoded = reader.ReadSingle();
            p.Camber = MathF.Abs(camberEncoded);
            p.Direction = MathF.Sign(camberEncoded);

            fastLane.Points.Add(p);
        }

        return fastLane;
    }

    public void Reverse()
    {
        Points.Reverse();

        foreach (var point in Points)
        {
            point.Forward *= -1;
            point.Direction *= -1;
        }
    }

    public void ToFile(Stream file)
    {
        using var writer = new BinaryWriter(file);
        writer.Write(Version);

        switch (Version)
        {
            case 7:
                ToFileV7(writer);
                break;
            case -1:
                ToFileVn1(writer);
                break;
            default:
                throw new InvalidOperationException($"Unknown fast lane version {Version}");
        }
    }

    private void ToFileVn1(BinaryWriter writer)
    {
        writer.Write(Points.Count);
        
        for (int i = 0; i < Points.Count; i++)
        {
            writer.Write(Points[i].Position.X);
            writer.Write(Points[i].Position.Y);
            writer.Write(Points[i].Position.Z);
            writer.Write(Points[i].Radius);
            writer.Write(Points[i].Camber * Points[i].Direction);
        }
    }

    private void ToFileV7(BinaryWriter writer)
    {
        writer.Write(Points.Count);
        writer.Write(LapTime);
        writer.Write(SampleCount);

        for (int i = 0; i < Points.Count; i++)
        {
            writer.Write(Points[i].Position.X);
            writer.Write(Points[i].Position.Y);
            writer.Write(Points[i].Position.Z);
            writer.Write(Points[i].Length);
            writer.Write(Points[i].Id);
        }
            
        writer.Write(Points.Count);

        for (int i = 0; i < Points.Count; i++)
        {
            writer.Write(Points[i].Speed);
            writer.Write(Points[i].Gas);
            writer.Write(Points[i].Brake);
            writer.Write(Points[i].ObsoleteLatG);
            writer.Write(Points[i].Radius);
            writer.Write(Points[i].SideLeft);
            writer.Write(Points[i].SideRight);
            writer.Write(Points[i].Camber);
            writer.Write(Points[i].Direction);
            writer.Write(Points[i].Normal.X);
            writer.Write(Points[i].Normal.Y);
            writer.Write(Points[i].Normal.Z);
            writer.Write(Points[i].DetailLength);
            writer.Write(Points[i].Forward.X);
            writer.Write(Points[i].Forward.Y);
            writer.Write(Points[i].Forward.Z);
            writer.Write(Points[i].Tag);
            writer.Write(Points[i].Grade);
        }
    }
}
