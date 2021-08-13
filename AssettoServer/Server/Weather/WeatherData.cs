using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssettoServer.Server.Weather
{
    public class WeatherData
    {
        public WeatherType Type { get; set; }
        public WeatherType UpcomingType { get; set; }
        public ushort TransitionValue { get; set; }
        public double TransitionValueInternal { get; set; }
        public double TransitionDuration { get; set; }
        public float TemperatureAmbient { get; set; }
        public float TemperatureRoad { get; set; }
        public int Pressure { get; set; }
        public int Humidity { get; set; }
        public float WindSpeed { get; set; }
        public int WindDirection { get; set; }
        public float RainIntensity { get; set; }
        public float RainWetness { get; set; }
        public float RainWater { get; set; }
    }
}
