using System.Globalization;
using System.IO;
using CsvHelper;
using Serilog;

namespace AssettoServer.Server.Ai
{
    public class JunctionFileRecord
    {
        public int Start { get; set; }
        public int End { get; set; }
        public float Probability { get; set; }
    }
    
    public class JunctionParser
    {
        public static void Parse(TrafficMap map, string path)
        {
            if (!File.Exists(path))
            {
                Log.Information("Junction file does not exist");
                return;
            }
            
            using (var reader = new StreamReader(path))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                var junctions = csv.GetRecords<JunctionFileRecord>();

                foreach (var junctionRecord in junctions)
                {
                    Log.Debug("J {0} -> {1} : {2}", junctionRecord.Start, junctionRecord.End, junctionRecord.Probability);

                    var startPoint = map.PointsById[junctionRecord.Start];
                    var endPoint = map.PointsById[junctionRecord.End];

                    var junction = new TrafficSplineJunction()
                    {
                        StartPoint = startPoint,
                        EndPoint = endPoint,
                        Probability = junctionRecord.Probability
                    };

                    startPoint.JunctionStart = junction;
                    endPoint.JunctionEnd = junction;
                }
            }
        }
    }
}