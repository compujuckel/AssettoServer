using System.IO;
using DotNext.IO;
using Serilog;

namespace AssettoServer.Server.Ai.Structs;

public static class AiSplineWriter
{
    public static void ToFile(MutableAiSpline map, string path)
    {
        using var file = File.Create(path);

        var treePoints = map.KdTree.InternalPointArray;
        var treeNodes = map.KdTree.InternalNodeArray;

        Log.Debug("Writing ai cache to file");
        file.Write(new AiSplineHeader
        {
            Version = 1,
            NumPoints = map.Points.Length,
            NumJunctions = map.Junctions.Count,
            NumKdTreePoints = treePoints.Length
        });

        for (int i = 0; i < map.Points.Length; i++)
        {
            file.Write(in map.Points[i]);
        }

        for (int i = 0; i < map.Junctions.Count; i++)
        {
            file.Write(map.Junctions[i]);
        }

        for (int i = 0; i < treePoints.Length; i++)
        {
            file.Write(in treePoints[i]);
        }
        
        for (int i = 0; i < treeNodes.Length; i++)
        {
            file.Write(in treeNodes[i]);
        }

        foreach (var lanes in map.Lanes)
        {
            file.Write(lanes.Length);
            for (int i = 0; i < lanes.Length; i++)
            {
                file.Write(in lanes[i]);
            }
        }
    }
}
