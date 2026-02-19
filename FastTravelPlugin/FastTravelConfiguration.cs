using AssettoServer.Server.Configuration;
using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace FastTravelPlugin;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class FastTravelConfiguration : IValidateConfiguration<FastTravelConfigurationValidator>
{
    [YamlMember(Description = "Requires CSP version 0.2.8 (3424) which fixed disabling collisions online. \nSetting this to false will lower the version requirement to 0.2.0 (2651) and clients will not have disabled collisions when teleporting.")]
    public bool DisableCollisions { get; set; } = true;

    [YamlMember(Description = "Available zoom levels. Last one should show the full track.\nIf map image is shown, prioritize matching the track to the map image.\nDon't change the values if using Shutoko Revival Project")]
    public List<int> MapZoomValues = [100, 1000, 4000, 15000];

    [YamlMember(Description = "Mouse move speeds of the respective zoom levels.\nLast value needs to be zero.\nDon't change the values if using Shutoko Revival Project")]
    public List<int> MapMoveSpeeds = [1, 5, 20, 0];

    [YamlMember(Description = "Show the map.png of the track layout when in the last zoom level.\nDon't change if using Shutoko Revival Project")]
    public bool ShowMapImage { get; set; } = true;

    [YamlMember(Description = "Last zoom level has a fixed position, the track should be aligned to the center of the screen.\nIf map image is shown, prioritize aligning the track with the map image.\nDon't change the values if using Shutoko Revival Project")]
    public List<int> MapFixedTargetPosition = [-2100, 0, 3200];

    [YamlMember(Description = "If set to true, points without a type inherit the type of the last explicitly typed point within their group.")]
    public bool UseGroupInheritance { get; set; } = true;

    [YamlMember(Description = "How teleport icons should cluster when zoomed out.\nTrue (Group mode): Displays only the first point of each type within a group.\nFalse (Distance mode): Displays one point of each type based on proximity, ignoring group names.")]
    public bool UseGroupDrawMode { get; set; } = true;

    [YamlMember(Description = "The required distance between icons of the same type to prevent them from clustering.")]
    public int DistanceModeRange { get; set; } = 100;
}
