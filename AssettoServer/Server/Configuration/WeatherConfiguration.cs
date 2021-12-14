using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AssettoServer.Server.Weather;

namespace AssettoServer.Server.Configuration
{
    public class WeatherConfiguration
    {
        public string Graphics { get; internal set; }
        public float BaseTemperatureAmbient { get; internal set; }
        public float BaseTemperatureRoad { get; internal set; }
        public float VariationAmbient { get; internal set; }
        public float VariationRoad { get; internal set; }
        public float WindBaseSpeedMin { get; internal set; }
        public float WindBaseSpeedMax { get; internal set; }
        public int WindBaseDirection { get; internal set; }
        public int WindVariationDirection { get; internal set; }
        public WeatherFxParams WeatherFxParams { get; internal set; }
    }
}
