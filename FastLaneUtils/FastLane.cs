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
        
        if (path.EndsWith(".aiz"))
        {
            using var compressed = new GZipStream(file, CompressionMode.Decompress);
            return FromFile(compressed);
        }
        else
        {
            return FromFile(file);
        }
    }
    
    public static FastLane FromFile(Stream file)
    {
        using var reader = new BinaryReader(file);

        var fastLane = new FastLane
        {
            Version = reader.ReadInt32(),
            DetailCount = reader.ReadInt32(),
            LapTime = reader.ReadInt32(),
            SampleCount = reader.ReadInt32()
        };

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

    public void NullUnused()
    {
        for (int i = 0; i < Points.Count; i++)
        {
            Points[i].Speed = 0;
            Points[i].Gas = 0;
            Points[i].Brake = 0;
            Points[i].ObsoleteLatG = 0;
            Points[i].SideLeft = 0;
            Points[i].SideRight = 0;
            Points[i].Normal = Vector3.Zero;
            Points[i].DetailLength = 0;
            Points[i].Forward = Vector3.Zero;
            Points[i].Tag = 0;
            Points[i].Grade = 0;
        }
    }

    public void ToFile(Stream file, bool compress = false)
    {
        if (compress)
        {
            using var compressed = new GZipStream(file, CompressionLevel.SmallestSize);
            ToFile(compressed);
        }
        else
        {
            using var writer = new BinaryWriter(file);
            
            writer.Write(Version);
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
}
