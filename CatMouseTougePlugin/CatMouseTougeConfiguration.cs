using System.Numerics;
using AssettoServer.Server.Configuration;
using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace CatMouseTougePlugin;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class CatMouseTougeConfiguration : IValidateConfiguration<CatMouseTougeConfigurationValidator>
{
    [YamlMember(Description = "Car performance ratings keyed by car model name. Can range from 1 - 1000.")]
    public Dictionary<string, int> CarPerformanceRatings { get; init; } = new Dictionary<string, int>
    {
        { "ks_mazda_miata", 125 },
        { "ks_toyota_ae86", 131 }
    };
    
    [YamlMember(Description = "Maximum elo gain. Must be a positive value.")]
    public int MaxEloGain { get; init; } = 32;
    
    [YamlMember(Description = "The starting positions for the touge races.")]
    public Dictionary<string, Vector3>[][] StartingPositions =
    [
    [
        new Dictionary<string, Vector3>
        {
            { "Position", new Vector3(-204.4f, 468.34f, -93.87f) },
            { "Direction", new Vector3(0.0998f, 0.992f, 0.0784f) }
        },
        new Dictionary<string, Vector3>
        {
            { "Position", new Vector3(-198.89f, 468.01f, -88.14f) },
            { "Direction", new Vector3(0.0919f, 0.992f, 0.0832f) }
        }
    ],
    ];
    
    [YamlMember(Description = "Number of races for which is player is marked as provisional for the elo system.")]
    public int ProvisionalRaces = 20;

    [YamlMember(Description = "Maximum elo gain, when player is marked as provisional")]
    public int MaxEloGainProvisional = 50;

    [YamlMember(Description = "Rolling start enabled.")]
    public bool isRollingStart = false;
}
