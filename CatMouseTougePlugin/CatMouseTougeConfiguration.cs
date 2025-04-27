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
}
