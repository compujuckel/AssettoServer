using AssettoServer.Server.Configuration;
using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace GeoIPPlugin;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class GeoIPConfiguration : IValidateConfiguration<GeoIPConfigurationValidator>
{
    [YamlMember(Description = "Path to GeoLite2-City.mmdb")]
    public string DatabasePath { get; set; } = null!;
}
