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
        public int BaseTemperatureAmbient { get; internal set; }
        public int BaseTemperatureRoad { get; internal set; }
        public int VariationAmbient { get; internal set; }
        public int VariationRoad { get; internal set; }
        public int WindBaseSpeedMin { get; internal set; }
        public int WindBaseSpeedMax { get; internal set; }
        public int WindBaseDirection { get; internal set; }
        public int WindVariationDirection { get; internal set; }
        public WeatherFxParams WeatherFxParams { get; internal set; }
    }
}
