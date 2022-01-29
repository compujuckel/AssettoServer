using YamlDotNet.Serialization;

namespace LiveWeatherPlugin;

public class LiveWeatherConfiguration
{
    public string? OpenWeatherMapApiKey { get; init; }
    public int UpdateIntervalMinutes { get; init; } = 10;

    [YamlIgnore] public int UpdateIntervalMilliseconds => UpdateIntervalMinutes * 60_000;
}