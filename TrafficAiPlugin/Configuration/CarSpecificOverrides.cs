using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace TrafficAiPlugin.Configuration;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class CarSpecificOverrides
{
    [YamlMember(Description = "Car model to match for these overrides")]
    public string? Model { get; init; }
    [YamlMember(Description = "AI spline height offset. Use this if the AI spline is too close to the ground")]
    public float? SplineHeightOffsetMeters { get; init; }
    [YamlMember(Description = "AI engine idle RPM")]
    public int? EngineIdleRpm { get; init; }
    [YamlMember(Description = "AI engine max RPM")]
    public int? EngineMaxRpm { get; init; }
    [YamlMember(Description = "AI car deceleration for obstacle/collision detection (m/s^2)")]
    public float? Acceleration { get; init; }
    [YamlMember(Description = "AI car acceleration for obstacle/collision detection (m/s^2)")]
    public float? Deceleration { get; init; }
    [YamlMember(Description = "AI cornering speed factor. Lower = AI cars will drive slower around corners.")]
    public float? CorneringSpeedFactor { get; init; }
    [YamlMember(Description = "AI cornering brake distance factor. Lower = AI cars will brake later for corners.")]
    public float? CorneringBrakeDistanceFactor { get; init; }
    [YamlMember(Description = "AI cornering brake force factor. This is multiplied with Deceleration. Lower = AI cars will brake less hard for corners.")]
    public float? CorneringBrakeForceFactor { get; init; }
    [YamlMember(Description = "Tyre diameter of AI cars in meters, shouldn't have to be changed unless cars are creating lots of smoke.")]
    public float? TyreDiameterMeters { get; set; }
    [YamlMember(Description = "Maximum number of AI states for a car slot of this car model")]
    public int? MaxOverbooking { get; set; }
    [YamlMember(Description = "Minimum time in which a newly spawned AI car cannot despawn")]
    public int? MinSpawnProtectionTimeSeconds { get; set; }
    [YamlMember(Description = "Maximum time in which a newly spawned AI car cannot despawn")]
    public int? MaxSpawnProtectionTimeSeconds { get; set; }
    [YamlMember(Description = "Minimum number of lanes needed to spawn a car of this car model")]
    public int? MinLaneCount { get; set; }
    [YamlMember(Description = "Maximum number of lanes needed to spawn a car of this car model")]
    public int? MaxLaneCount { get; set; }
    [YamlMember(Description = "Minimum time an AI car will stop/slow down after a collision")]
    public int? MinCollisionStopTimeSeconds { get; set; }
    [YamlMember(Description = "Maximum time an AI car will stop/slow down after a collision")]
    public int? MaxCollisionStopTimeSeconds { get; set; }
    [YamlMember(Description = "Length of this vehicle in front of car origin")]
    public float? VehicleLengthPreMeters { get; set; }
    [YamlMember(Description = "Length of this vehicle behind car origin")]
    public float? VehicleLengthPostMeters { get; set; }
    [YamlMember(Description = "Minimum distance between AI cars")]
    public int? MinAiSafetyDistanceMeters { get; set; }
    [YamlMember(Description = "Maximum distance between AI cars")]
    public int? MaxAiSafetyDistanceMeters { get; set; }
    [YamlMember(Description = "List of allowed lanes for this car model. Possible values Left, Middle, Right")]
    public List<LaneSpawnBehavior>? AllowedLanes { get; set; }

    [YamlIgnore] public int? MinSpawnProtectionTimeMilliseconds => MinSpawnProtectionTimeSeconds * 1000;
    [YamlIgnore] public int? MaxSpawnProtectionTimeMilliseconds => MaxSpawnProtectionTimeSeconds * 1000;
    [YamlIgnore] public int? MinCollisionStopTimeMilliseconds => MinCollisionStopTimeSeconds * 1000;
    [YamlIgnore] public int? MaxCollisionStopTimeMilliseconds => MaxCollisionStopTimeSeconds * 1000;
    [YamlIgnore] public int? MinAiSafetyDistanceMetersSquared => MinAiSafetyDistanceMeters * MinAiSafetyDistanceMeters;
    [YamlIgnore] public int? MaxAiSafetyDistanceMetersSquared => MaxAiSafetyDistanceMeters * MaxAiSafetyDistanceMeters;
}
