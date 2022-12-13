using System.IO;
using DotNext.IO;
using Serilog;

namespace AssettoServer.Server.Ai.Structs;

public static class AiCacheWriter
{
    public static void ToFile(AiPackage map, string path)
    {
        using var file = File.Create(path);

        Log.Debug("Writing ai cache to file");
        file.Write(new AiCacheHeader
        {
            Version = 1,
            NumPoints = map.PointsById.Length,
            NumJunctions = map.Junctions.Count
        });

        for (int i = 0; i < map.PointsById.Length; i++)
        {
            file.Write(in map.PointsById[i]);
        }

        for (int i = 0; i < map.Junctions.Count; i++)
        {
            file.Write(map.Junctions[i]);
        }
    }
}
