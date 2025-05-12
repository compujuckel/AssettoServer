using System.Numerics;
using AssettoServer.Server.Configuration;
using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace TougePlugin;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class TougeConfiguration : IValidateConfiguration<TougeConfigurationValidator>
{
    [YamlMember(Description = "Car performance ratings keyed by car model name. Can range from 1 - 1000.")]
    public Dictionary<string, int> CarPerformanceRatings { get; init; } = new Dictionary<string, int>
    {
        { "ks_mazda_miata", 125 },
        { "ks_toyota_ae86", 131 }
    };
    
    [YamlMember(Description = "Maximum elo gain. Must be a positive value.")]
    public int MaxEloGain { get; init; } = 32;
    
    [YamlMember(Description = "Number of races for which is player is marked as provisional for the elo system.")]
    public int ProvisionalRaces = 20;

    [YamlMember(Description = "Maximum elo gain, when player is marked as provisional")]
    public int MaxEloGainProvisional = 50;

    [YamlMember(Description = "Rolling start enabled.")]
    public bool isRollingStart = false;

    [YamlMember(Description = "Outrun timer in seconds. Chase car has to finish within this amount of time after the lead car crosses the finish line.")]
    public int outrunTime = 3;

    [YamlMember(Description = "Local database mode enabled. If disabled please provide database connection information.")]
    public bool isDbLocalMode = true;

    [YamlMember(Description = "Connection string to PostgreSQL database. Can be left empty if isDbLocalMode = true.")]
    public string? postgresqlConnectionString;
}
