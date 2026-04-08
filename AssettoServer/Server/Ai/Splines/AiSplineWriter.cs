using System.IO;
using AssettoServer.Utils;
using Serilog;

namespace AssettoServer.Server.Ai.Splines;

public class AiSplineWriter
{
    public void ToFile(MutableAiSpline map, string path)
    {
        Log.Debug("Writing cached AI spline to file");
        using var file = File.Create(path);

        var treePoints = map.KdTree.InternalPointArray;
        var treeNodes = map.KdTree.InternalNodeArray;

        file.Write(new AiSplineHeader
        {
            Version = 1,
            NumPoints = map.Points.Length,
            NumJunctions = map.Junctions.Count,
            NumKdTreePoints = treePoints.Length
        });
        
        file.Write(map.Points);
        file.Write(map.Junctions);
        file.Write(treePoints);
        file.Write(treeNodes);

        foreach (var lanes in map.Lanes)
        {
            file.Write(lanes.Length);
            file.Write(lanes);
        }
    }
}
