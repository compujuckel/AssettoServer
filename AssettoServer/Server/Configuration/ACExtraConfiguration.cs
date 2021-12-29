using System.Collections.Generic;
using AssettoServer.Server.Weather;
using YamlDotNet.Serialization;

namespace AssettoServer.Server.Configuration
{
    public class ACExtraConfiguration
    {
        [YamlMember(Description = "Enable Steam ticket validation. Requires CSP 0.1.75+ and a recent version of Content Manager")]
        public bool UseSteamAuth { get; set; } = false;
        [YamlMember(Description = "List of DLC App IDs that are required to join. Steam auth must be enabled. Possible values: https://steamdb.info/app/244210/dlc/")]
        public List<int> ValidateDlcOwnership { get; set; }= new();
        [YamlMember(Description = "Enable AFK autokick")]
        public bool EnableAntiAfk { get; set; } = true;
        [YamlMember(Description = "Maximum allowed AFK time before kick")]
        public int MaxAfkTimeMinutes { get; set; } = 10;
        [YamlMember(Description = "Players might try to get around the AFK kick by doing inputs once in a while without actually driving. Set this to MininumSpeed to autokick players idling")]
        public AfkKickBehavior AfkKickBehavior { get; set; } = AfkKickBehavior.PlayerInput;
        [YamlMember(Description = "Maximum ping before autokick")]
        public int MaxPing { get; set; } = 500;
        [YamlMember(Description = "Maximum ping duration before autokick")]
        public int MaxPingSeconds { get; set; } = 10;
        [YamlMember(Description = "Force headlights on for all cars")]
        public bool ForceLights { get; set; }
        [YamlMember(Description = "Distance for network optimizations. Players outside of this range will send less updates to reduce network traffic")]
        public float NetworkBubbleDistance { get; set; } = 500;
        [YamlMember(Description = "Refresh rate for players outside of the network bubble")]
        public int OutsideNetworkBubbleRefreshRateHz { get; set; } = 4;
        [YamlMember(Description = "Enable server details in CM. Required for server description")]
        public bool EnableServerDetails { get; set; } = true;
        [YamlMember(Description = "Server description shown in Content Manager. EnableServerDetails must be on")]
        public string ServerDescription { get; set; } = "";
        [YamlMember(Description = "Link server time to real map time. For correct timezones there must be an entry for the map here: https://github.com/ac-custom-shaders-patch/acc-extension-config/blob/master/config/data_track_params.ini")]
        public bool EnableRealTime { get; set; } = false;
        [YamlMember(Description = "Enable new CSP weather handling. Allows rain and smooth weather transitions. Requires CSP 0.1.76+")]
        public bool EnableWeatherFx { get; set; } = false;
        [YamlMember(Description = "Reduce track grip when the track is wet. This is much worse than proper CSP rain physics but allows you to run clients with public/Patreon CSP at the same time")]
        public double RainTrackGripReductionPercent { get; set; } = 0;
        [YamlMember(Description = "Enable AI traffic")]
        public bool EnableAi { get; set; } = false;
        [YamlMember(Description = "Override the country shown in CM. Please do not use this unless the autodetected country is wrong")]
        public List<string> GeoParamsCountryOverride { get; set; } = null;
        [YamlMember(Description = "List of plugins to enable")]
        public List<string> EnablePlugins { get; set; } = new();
        [YamlMember(Description = "Ignore some common configuration errors. More info: https://github.com/compujuckel/AssettoServer/wiki/Common-configuration-errors")]
        public IgnoreConfigurationErrors IgnoreConfigurationErrors { get; set; } = new();
        public AiParams AiParams { get; set; } = new AiParams();

        [YamlIgnore] public int MaxAfkTimeMilliseconds => MaxAfkTimeMinutes * 60_000;
    }

    public class IgnoreConfigurationErrors
    {
        public bool MissingCarChecksums { get; set; } = false;
        public bool MissingTrackParams { get; set; } = false;
        public bool WrongServerDetails { get; set; } = false;
        public bool UnsafeAdminWhitelist { get; set; } = false;
    }

    public class AiParams
    {
        [YamlMember(Description = "Radius around a player in which AI cars won't despawn")]
        public float PlayerRadiusMeters { get; set; } = 200.0f;
        [YamlMember(Description = "Offset the player radius in direction of the velocity of the player so AI cars will despawn earlier behind a player")]
        public float PlayerPositionOffsetMeters { get; set; } = 100.0f;
        [YamlMember(Description = "AFK timeout for players. Players who are AFK longer than this won't spawn AI cars")]
        public long PlayerAfkTimeoutSeconds { get; set; } = 10;
        [YamlMember(Description = "Maximum distance to the AI spline for a player to spawn AI cars. This helps with parts of the map without traffic so AI cars won't spawn far away from players")]
        public float MaxPlayerDistanceToAiSplineMeters { get; set; } = 7;
        [YamlMember(Description = "Minimum amount of spline points in front of a player where AI cars will spawn")]
        public int MinSpawnDistancePoints { get; set; } = 100;
        [YamlMember(Description = "Maximum amount of spline points in front of a player where AI cars will spawn")]
        public int MaxSpawnDistancePoints { get; set; } = 400;
        [YamlMember(Description = "Minimum distance between AI cars")]
        public int MinAiSafetyDistanceMeters { get; set; } = 20;
        [YamlMember(Description = "Maximum distance between AI cars")]
        public int MaxAiSafetyDistanceMeters { get; set; } = 70;
        [YamlMember(Description = "Minimum spawn distance for AI states of the same car slot. If you set this too low you risk AI states despawning or AI states becoming invisible for some players when multiple states are close together")]
        public float StateSpawnDistanceMeters { get; set; } = 1000;
        [YamlMember(Description = "Minimum distance between AI states of the same car slot. If states get closer than this one of them will be forced to despawn")]
        public float MinStateDistanceMeters { get; set; } = 200;
        [YamlMember(Description = "")]
        public float StateTieBreakerDistanceMeters { get; set; } = 250;
        [YamlMember(Description = "Minimum spawn distance to players")]
        public float SpawnSafetyDistanceToPlayerMeters { get; set; } = 100;
        [YamlMember(Description = "Minimum time in which a newly spawned AI car cannot despawn")]
        public int MinSpawnProtectionTimeSeconds { get; set; } = 4;
        [YamlMember(Description = "Maximum time in which a newly spawned AI car cannot despawn")]
        public int MaxSpawnProtectionTimeSeconds { get; set; } = 8;
        [YamlMember(Description = "Minimum time an AI car will stop/slow down after a collision")]
        public int MinCollisionStopTimeSeconds { get; set; } = 1;
        [YamlMember(Description = "Maximum time an AI car will stop/slow down after a collision")]
        public int MaxCollisionStopTimeSeconds { get; set; } = 3;
        [YamlMember(Description = "Default maximum AI speed. This is not an absolute maximum and can be overridden with RightLaneOffsetKph and MaxSpeedVariationPercent")]
        public float MaxSpeedKph { get; set; } = 80;
        [YamlMember(Description = "Speed offset for right lanes. Will be added to MaxSpeedKph")]
        public float RightLaneOffsetKph { get; set; } = 10;
        [YamlMember(Description = "Maximum speed variation")]
        public float MaxSpeedVariationPercent { get; set; } = 0.15f;
        [YamlMember(Description = "Default AI car deceleration for obstacle/collision detection (m/s^2)")]
        public float DefaultDeceleration { get; set; } = 8.5f;
        [YamlMember(Description = "Default AI car acceleration for obstacle/collision detection (m/s^2)")]
        public float DefaultAcceleration { get; set; } = 2.5f;
        [YamlMember(Description = "Maximum AI car target count for AI slot overbooking. This is not an absolute maximum and might be slightly higher")]
        public int MaxAiTargetCount { get; set; } = 300;
        [YamlMember(Description = "Number of AI cars per player the server will try to keep")]
        public int AiPerPlayerTargetCount { get; set; } = 10;
        [YamlMember(Description = "Soft player limit, the server will stop accepting new players when this many players are reached. Use this to ensure a minimum amount of AI cars. 0 to disable.")]
        public int MaxPlayerCount { get; set; } = 0;
        [YamlMember(Description = "Hide AI car nametags and make them invisible on the minimap. CSP 0.1.76+ required, still buggy")]
        public bool HideAiCars { get; set; } = false;
        [YamlMember(Description = "AI spline height offset. Use this if the AI spline is too close to the ground")]
        public float SplineHeightOffsetMeters { get; set; } = 0;
        [YamlMember(Description = "Lane width for adjacent lane detection")]
        public float LaneWidthMeters { get; set; } = 3.0f;
        [YamlMember(Description = "Enable two way traffic. This will allow AI cars to spawn in lanes with the opposite direction of travel to the player.")]
        public bool TwoWayTraffic { get; set; } = false;
        [YamlMember(Description = "AI cornering speed factor. Lower = AI cars will drive slower around corners.")]
        public float CorneringSpeedFactor { get; set; } = 1;
        [YamlMember(Description = "AI cornering brake distance factor. Lower = AI cars will brake later for corners.")]
        public float CorneringBrakeDistanceFactor { get; set; } = 1;
        [YamlMember(Description = "AI cornering brake force factor. This is multiplied with DefaultDeceleration. Lower = AI cars will brake less hard for corners.")]
        public float CorneringBrakeForceFactor { get; set; } = 1;
        [YamlMember(Description = "Name prefix for AI cars. Names will be in the form of '<NamePrefix> <SessionId>'")]
        public string NamePrefix { get; set; } = "Traffic";
        [YamlMember(Description = "Override some settings for specific car models/skins")]
        public List<CarSpecificOverrides> CarSpecificOverrides { get; set; } = new();

        [YamlIgnore] public float PlayerRadiusSquared => PlayerRadiusMeters * PlayerRadiusMeters;
        [YamlIgnore] public float PlayerAfkTimeoutMilliseconds => PlayerAfkTimeoutSeconds * 1000;
        [YamlIgnore] public float MaxPlayerDistanceToAiSplineSquared => MaxPlayerDistanceToAiSplineMeters * MaxPlayerDistanceToAiSplineMeters;
        [YamlIgnore] public int MinAiSafetyDistanceSquared => MinAiSafetyDistanceMeters * MinAiSafetyDistanceMeters;
        [YamlIgnore] public int MaxAiSafetyDistanceSquared => MaxAiSafetyDistanceMeters * MaxAiSafetyDistanceMeters;
        [YamlIgnore] public float StateSpawnDistanceSquared => StateSpawnDistanceMeters * StateSpawnDistanceMeters;
        [YamlIgnore] public float MinStateDistanceSquared => MinStateDistanceMeters * MinStateDistanceMeters;
        [YamlIgnore] public float StateTieBreakerDistanceSquared => StateTieBreakerDistanceMeters * StateTieBreakerDistanceMeters;
        [YamlIgnore] public float SpawnSafetyDistanceToPlayerSquared => SpawnSafetyDistanceToPlayerMeters * SpawnSafetyDistanceToPlayerMeters;
        [YamlIgnore] public int MinSpawnProtectionTimeMilliseconds => MinSpawnProtectionTimeSeconds * 1000;
        [YamlIgnore] public int MaxSpawnProtectionTimeMilliseconds => MaxSpawnProtectionTimeSeconds * 1000;
        [YamlIgnore] public int MinCollisionStopTimeMilliseconds => MinCollisionStopTimeSeconds * 1000;
        [YamlIgnore] public int MaxCollisionStopTimeMilliseconds => MaxCollisionStopTimeSeconds * 1000;
        [YamlIgnore] public float MaxSpeedMs => MaxSpeedKph / 3.6f;
        [YamlIgnore] public float RightLaneOffsetMs => RightLaneOffsetKph / 3.6f;
    }

    public class CarSpecificOverrides
    {
        [YamlMember(Description = "Car model to match for these overrides")]
        public string Model { get; set; }
        [YamlMember(Description = "AI spline height offset. Use this if the AI spline is too close to the ground")]
        public float? SplineHeightOffsetMeters { get; set; }
        [YamlMember(Description = "AI engine idle RPM")]
        public int? EngineIdleRpm { get; set; }
        [YamlMember(Description = "AI engine max RPM")]
        public int? EngineMaxRpm { get; set; }
        [YamlMember(Description = "Disallow random color changes after respawn")]
        public bool DisableColorChanges { get; set; } = false;
        [YamlMember(Description = "Override some settings for specific skins of this car model")]
        public List<SkinSpecificOverrides> SkinSpecificOverrides { get; set; } = new();
    }

    public class SkinSpecificOverrides
    {
        [YamlMember(Description = "Skin to match for these overrides")]
        public string Skin { get; set; }
        [YamlMember(Description = "Disallow random color changes after respawn")]
        public bool DisableColorChanges { get; set; } = false;
    }

    public enum AfkKickBehavior
    {
        PlayerInput,
        MinimumSpeed
    }
}
