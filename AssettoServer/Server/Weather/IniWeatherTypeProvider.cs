using IniParser;
using IniParser.Model;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssettoServer.Server.Weather
{
    public class IniWeatherTypeProvider : IWeatherTypeProvider
    {
        private const string _weatherPath = "content/weather/";
        private Logger Log { get; }
        private Dictionary<CMWeatherType, WeatherType> _weatherTypes;
        public IniWeatherTypeProvider(Logger log)
        {
            Log = log;

            _weatherTypes = new Dictionary<CMWeatherType, WeatherType>();

            try
            {
                foreach (string path in Directory.GetDirectories(_weatherPath))
                {
                    var parser = new FileIniDataParser();
                    IniData data = parser.ReadFile(Path.Combine(path, "weather.ini"));

                    string weatherTypeIdStr = data?["__LAUNCHER_CM"]?["WEATHER_TYPE"];

                    if (weatherTypeIdStr != null)
                    {
                        CMWeatherType cmWeatherType = (CMWeatherType)int.Parse(weatherTypeIdStr);
                        var weather = new WeatherType
                        {
                            WeatherTypeId = cmWeatherType,
                            Name = data["LAUNCHER"]["NAME"],
                            Graphics = new DirectoryInfo(path).Name,
                            TemperatureCoefficient = float.Parse(data["LAUNCHER"]["TEMPERATURE_COEFF"] ?? "0")
                        };

                        if (!_weatherTypes.ContainsKey(cmWeatherType))
                        {
                            Log.Debug("Loaded weather {0} ({1}), coeff {2}", weather.Name, weather.WeatherTypeId, weather.TemperatureCoefficient);
                            _weatherTypes.Add(cmWeatherType, weather);

                        }
                        else
                        {
                            Log.Warning("Cannot add weather {0}. A weather with WEATHER_TYPE {1} already exists", path, cmWeatherType);
                        }

                    }
                    else
                    {
                        Log.Warning("Weather {0} has no WEATHER_TYPE set", path);
                    }
                }
            }
            catch (DirectoryNotFoundException e)
            {
                Log.Fatal(e, "Error loading weather data. For live weather to work, you need to copy the content/weather folder from SOL into your AssettoServer folder.");
                throw;
            }
        }

        public WeatherType GetWeatherType(CMWeatherType id)
        {
            WeatherType ret;
            if(_weatherTypes.TryGetValue(id, out ret))
            {
                return ret;
            }

            Log.Warning("No weather found for id {0}, falling back to default");
            return new WeatherType
            {
                WeatherTypeId = CMWeatherType.Clear,
                Name = "Clear",
                Graphics = "3_clear",
                TemperatureCoefficient = 0
            };
        }
    }
}
