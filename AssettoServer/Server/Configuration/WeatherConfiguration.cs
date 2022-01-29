using AssettoServer.Server.Weather;

namespace AssettoServer.Server.Configuration
{
    public class WeatherConfiguration
    {
        public string Graphics { get; init; }
        public float BaseTemperatureAmbient { get; init; }
        public float BaseTemperatureRoad { get; init; }
        public float VariationAmbient { get; init; }
        public float VariationRoad { get; init; }
        public float WindBaseSpeedMin { get; init; }
        public float WindBaseSpeedMax { get; init; }
        public int WindBaseDirection { get; init; }
        public int WindVariationDirection { get; init; }
        public WeatherFxParams WeatherFxParams { get; init; }

        public WeatherConfiguration(string graphics)
        {
            Graphics = graphics;
            WeatherFxParams = WeatherFxParams.FromString(graphics);
        }
    }
}
