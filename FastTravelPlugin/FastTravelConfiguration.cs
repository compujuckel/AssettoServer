using AssettoServer.Server.Configuration;
using JetBrains.Annotations;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace FastTravelPlugin;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class FastTravelConfiguration : IValidateConfiguration<FastTravelConfigurationValidator>
{
    [YamlMember(Description = "Requires CSP version 0.2.8 (3424) which fixed disabling collisions online. \nSetting this to false will lower the version requirement to 0.2.0 (2651) but clients on versions below 0.2.3-preview211 will not have disabled collisions")]
    public bool RequireCollisionDisable { get; set; } = true;

    [YamlMember(Description = "Available zoom levels. Last one should show the full track.\nIf map image is shown, prioritize matching the track to the map image.\nDon't change the values if using Shutoko Revival Project")]
    public List<int> MapZoomValues = [100, 1000, 4000, 15000];

    [YamlMember(Description = "Mouse move speeds of the respective zoom levels.\nLast value needs to be zero.\nDon't change the values if using Shutoko Revival Project")]
    public List<int> MapMoveSpeeds = [1, 5, 20, 0];

    [YamlMember(Description = "Show the map.png of the track layout when in the last zoom level.\nDon't change if using Shutoko Revival Project")]
    public bool ShowMapImage = true;

    [YamlMember(Description = "Last zoom level has a fixed position, the track should be aligned to the center of the screen.\nIf map image is shown, prioritize aligning the track with the map image.\nDon't change the values if using Shutoko Revival Project")] 
    public List<int> MapFixedTargetPosition = [-2100, 0, 3200];
}
