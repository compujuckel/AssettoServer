using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssettoServer.Server.Weather
{
    public class WeatherProviderResponse
    {
        public CMWeatherType WeatherType;
        public float TemperatureAmbient;
        public int Pressure;
        public int Humidity;
        public float WindSpeed;
        public int WindDirection;
    }
}
