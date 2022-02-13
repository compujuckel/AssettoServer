using System.IO;
using AssettoServer.Server.Configuration;
using Serilog;
using YamlDotNet.Serialization;

namespace AssettoServer.Server.Ai;

public class JunctionParser
{
    public static void Parse(TrafficMap map, string path)
    {
        if (!File.Exists(path))
        {
            Log.Information("Junction file does not exist");
            return;
        }

        var deserializer = new DeserializerBuilder().Build();

        using var file = File.OpenText(path);
        var config = deserializer.Deserialize<TrafficConfiguration>(file);

        foreach (var spline in config.Splines)
        {
            var startSpline = map.Splines.Find(s => s.Name == spline.Name) ?? throw new ConfigurationException($"Could not find spline with name {spline.Name}");

            foreach (var junction in spline.Junctions)
            {
                Log.Debug("Junction {Name} from {StartSpline} {StartId} to {EndSpline} {EndId}", junction.Name, startSpline.Name, junction.Start, junction.EndSpline, junction.End);
                var endSpline = map.Splines.Find(s => s.Name == junction.EndSpline) ?? throw new ConfigurationException($"Could not find spline with name {spline.Name}");

                var startPoint = startSpline.Points[junction.Start];
                var endPoint = endSpline.Points[junction.End];

                var jct = new TrafficSplineJunction
                {
                    StartPoint = startPoint,
                    EndPoint = endPoint,
                    Probability = junction.Probability
                };

                startPoint.JunctionStart = jct;
                endPoint.JunctionEnd = jct;
            }
        }
    }
}
