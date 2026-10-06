using System.Text.Json.Serialization;

namespace LiveWeatherPlugin;

public class OpenWeatherMapError
{
    [JsonPropertyName("cod")]
    public int Code { get; init; }
    [JsonPropertyName("message")]
    public required string Message { get; init; }
}

public class OpenWeatherMapResponse
{
    [JsonPropertyName("weather")]
    public required IEnumerable<OpenWeatherMapResponseWeather> Weather { get; init; }
    [JsonPropertyName("main")]
    public required OpenWeatherMapResponseMain Main { get; init; }
    [JsonPropertyName("wind")]
    public required OpenWeatherMapResponseWind Wind { get; init; }

    public class OpenWeatherMapResponseWeather
    {
        [JsonPropertyName("id")]
        public int Id { get; init; }
    }

    public class OpenWeatherMapResponseMain
    {
        [JsonPropertyName("temp")]
        public float Temperature { get; init; }
        [JsonPropertyName("pressure")]
        public int Pressure { get; init; }
        [JsonPropertyName("humidity")]
        public int Humidity { get; init; }
    }

    public class OpenWeatherMapResponseWind
    {
        [JsonPropertyName("speed")]
        public float Speed { get; init; }
        [JsonPropertyName("deg")]
        public int Direction { get; init; }
    }
}
