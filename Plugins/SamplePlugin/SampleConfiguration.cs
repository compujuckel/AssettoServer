using AssettoServer.Server.Configuration;
using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace SamplePlugin;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class SampleConfiguration : IValidateConfiguration<SampleConfigurationValidator>
{
    [YamlMember(Description = "Sample description")]
    public string Hello { get; init; } = "World!";
}
