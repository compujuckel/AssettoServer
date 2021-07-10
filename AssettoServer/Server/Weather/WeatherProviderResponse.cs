using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssettoServer.Server.Weather
{
    public class WeatherProviderResponse
    {
        public WeatherFxType WeatherType { get; init; }
        public float TemperatureAmbient { get; init; }
        public int Pressure { get; init; }
        public int Humidity { get; init; }
        public float WindSpeed { get; init; }
        public int WindDirection { get; init; }
    }
}
