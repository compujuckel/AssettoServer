using AssettoServer.Utils;
using Serilog;

namespace TrafficAiPlugin.Splines;

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
