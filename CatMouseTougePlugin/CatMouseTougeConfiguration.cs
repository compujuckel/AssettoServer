using AssettoServer.Server.Configuration;
using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace CatMouseTougePlugin;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class CatMouseTougeConfiguration : IValidateConfiguration<CatMouseTougeConfigurationValidator>
{
    [YamlMember(Description = "Cat mouse touge description")]
    public string Message { get; init; } = "World!";
}
