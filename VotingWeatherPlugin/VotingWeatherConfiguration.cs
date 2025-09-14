using AssettoServer.Server.Configuration;
using AssettoServer.Shared.Weather;
using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace VotingWeatherPlugin;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class VotingWeatherConfiguration : IValidateConfiguration<VotingWeatherConfigurationValidator>
{
    [YamlMember(Description = "List of weather types that can't be voted on")]
    public List<WeatherFxType> BlacklistedWeathers { get; init; } = [];
    [YamlMember(Description = "Number of choices players can choose from at each voting interval")]
    public int NumChoices { get; init; } = 3;
    [YamlMember(Description = "How often a vote takes place")]
    public int VotingIntervalMinutes { get; init; } = 10;
    [YamlMember(Description = "How long the vote stays open")]
    public int VotingDurationSeconds { get; init; } = 30;
    
    [YamlMember(Description = "Should the weather be kept when no vote has been counted")]
    public bool KeepWeatherOnNoVotes { get; init; } = false;

    [YamlIgnore] public int VotingIntervalMilliseconds => VotingIntervalMinutes * 60_000;
    [YamlIgnore] public int VotingDurationMilliseconds => VotingDurationSeconds * 1000;
}
