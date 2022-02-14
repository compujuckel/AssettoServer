using System.IO;
using Serilog;
using YamlDotNet.Serialization;

namespace AssettoServer.Server.Ai;

public static class TrafficConfigurationParser
{
    public static void Parse(TrafficMap map, string path)
    {
        if (!File.Exists(path))
        {
            Log.Information("Traffic configuration does not exist");
            return;
        }

        var deserializer = new DeserializerBuilder().Build();

        using var file = File.OpenText(path);
        var config = deserializer.Deserialize<TrafficConfiguration>(file);

        foreach (var spline in config.Splines)
        {
            var startSpline = map.Splines[spline.Name];

            if (spline.ConnectEnd != null)
            {
                var endPoint = map.GetByIdentifier(spline.ConnectEnd);
                var startPoint = startSpline.Points[^1];
                startPoint.Next = endPoint;
                
                var jct = new TrafficSplineJunction
                {
                    StartPoint = startPoint,
                    EndPoint = endPoint,
                    Probability = 1.0f
                };

                startPoint.JunctionStart = jct;
                endPoint.JunctionEnd = jct;
            }

            foreach (var junction in spline.Junctions)
            {
                Log.Debug("Junction {Name} from {StartSpline} {StartId} to {End}", junction.Name, startSpline.Name, junction.Start, junction.End);

                var startPoint = startSpline.Points[junction.Start];
                var endPoint = map.GetByIdentifier(junction.End);

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
