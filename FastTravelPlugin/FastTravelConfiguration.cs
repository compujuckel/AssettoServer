using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace FastTravelPlugin;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class FastTravelConfiguration
{
    [YamlMember(Description = "Requires CSP version 0.2.3-preview211 (2974) which fixed disabling collisions online. \nSetting this to false will lower the version requirement to 0.2.0 (2651) but clients on versions below 0.2.3-preview211 will not have disabled collisions")]
    public bool RequireCollisionDisable { get; set; } = true;
}
