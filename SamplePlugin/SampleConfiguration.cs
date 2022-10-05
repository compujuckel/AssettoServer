using AssettoServer.Server.Configuration;
using JetBrains.Annotations;

namespace SamplePlugin;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class SampleConfiguration : IValidateConfiguration<SampleConfigurationValidator>
{
    public string Hello { get; init; } = "World!";
}
