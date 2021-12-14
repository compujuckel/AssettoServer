using System;
using System.Text;

namespace AssettoServer.Server.Weather
{
    public class WeatherFxParams
    {
        public WeatherFxType Type { get; init; }
        public long? StartDate { get; init; }
        public int? StartTime { get; init; }
        public double? TimeMultiplier { get; init; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            
            sb.AppendFormat("3_clear_type={0}", (int)Type);

            if (StartDate.HasValue)
            {
                sb.AppendFormat("_start={0}", StartDate);
            }

            if (StartTime.HasValue)
            {
                sb.AppendFormat("_time={0}", StartTime);
            }

            if (TimeMultiplier.HasValue)
            {
                sb.AppendFormat("_mult={0}", TimeMultiplier);
            }

            return sb.ToString();
        }

        public static WeatherFxParams FromString(string input)
        {
            string[] pairs = input.Split("_");

            WeatherFxType type = WeatherFxType.None;
            long? startDate = null;
            int? startTime = null;
            double? timeMultiplier = null;

            foreach (string pair in pairs)
            {
                string[] kv = pair.Split("=");

                if (kv.Length == 2)
                {
                    if (kv[0] == "type")
                    {
                        Enum.TryParse(kv[1], out type);
                    }

                    if (kv[0] == "start")
                    {
                        startDate = long.Parse(kv[1]);
                    }

                    if (kv[0] == "time")
                    {
                        startTime = int.Parse(kv[1]);
                    }

                    if (kv[0] == "mult")
                    {
                        timeMultiplier = double.Parse(kv[1]);
                    }
                }
            }

            return new WeatherFxParams()
            {
                Type = type,
                StartDate = startDate,
                StartTime = startTime,
                TimeMultiplier = timeMultiplier
            };
        }
    }
}