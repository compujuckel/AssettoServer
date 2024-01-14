using AssettoServer.Server.Configuration;
using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace LiveWeatherPlugin;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class LiveWeatherConfiguration : IValidateConfiguration<LiveWeatherConfigurationValidator>
{
    [YamlMember(Description = "OpenWeatherMap API key")]
    public string OpenWeatherMapApiKey { get; init; } = null!;
    [YamlMember(Description = "How often the weather is updated")]
    public int UpdateIntervalMinutes { get; init; } = 10;

    [YamlIgnore] public int UpdateIntervalMilliseconds => UpdateIntervalMinutes * 60_000;
}
