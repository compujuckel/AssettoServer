using AssettoServer.Server.Configuration;
using JetBrains.Annotations;

namespace GeoIPPlugin;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class GeoIPConfiguration : IValidateConfiguration<GeoIPConfigurationValidator>
{
    public string DatabasePath { get; set; } = null!;
}
