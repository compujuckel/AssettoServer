using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Reflection;
using AssettoServer.Server.Plugin;
using Autofac;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentValidation;
using JetBrains.Annotations;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NodeTypeResolvers;
#pragma warning disable CS0657

namespace AssettoServer.Server.Configuration;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public partial class ACExtraConfiguration : ObservableObject
{
    [YamlMember(Description = "Enable Steam ticket validation. Requires CSP 0.1.75+ and a recent version of Content Manager")]
    public bool UseSteamAuth { get; init; } = false;
    [YamlMember(Description = "List of DLC App IDs that are required to join. Steam auth must be enabled. Possible values: https://steamdb.info/app/244210/dlc/")]
    public List<int> ValidateDlcOwnership { get; init; } = [];
    [YamlMember(Description = "Enable protection against cheats/hacks. 0 = No protection. 1 = Block all public cheats as of 2023-11-18 (ClientSecurityPlugin and CSP 0.2.0+ required)")]
    public int MandatoryClientSecurityLevel { get; init; }
    [YamlMember(Description = "Enable AFK autokick")]
    public bool EnableAntiAfk { get; set; } = true;
    [YamlMember(Description = "Maximum allowed AFK time before kick")]
    public int MaxAfkTimeMinutes { get; set; } = 10;
    [YamlMember(Description = "Players might try to get around the AFK kick by doing inputs once in a while without actually driving. Set this to MinimumSpeed to autokick players idling")]
    public AfkKickBehavior AfkKickBehavior { get; set; } = AfkKickBehavior.PlayerInput;
    [YamlMember(Description = "Maximum ping before autokick")]
    public int MaxPing { get; set; } = 500;
    [YamlMember(Description = "Maximum ping duration before autokick")]
    public int MaxPingSeconds { get; set; } = 10;
    [YamlMember(Description = "Force headlights on for all cars")]
    public bool ForceLights { get; set; }
    [YamlMember(Description = "Distance for network optimizations. Players outside of this range will send less updates to reduce network traffic")]
    public float NetworkBubbleDistance { get; init; } = 500;
    [YamlMember(Description = "Refresh rate for players outside of the network bubble")]
    public int OutsideNetworkBubbleRefreshRateHz { get; init; } = 4;
    [YamlMember(Description = "Enable server details in CM. Required for server description")]
    public bool EnableServerDetails { get; set; } = true;
    [YamlMember(Description = "Server description shown in Content Manager. EnableServerDetails must be on")]
    public string ServerDescription { get; set; } = "";
    [YamlMember(Description = "Link server time to real map time. For correct timezones there must be an entry for the map here: https://github.com/ac-custom-shaders-patch/acc-extension-config/blob/master/config/data_track_params.ini")]
    public bool EnableRealTime { get; set; } = false;
    [YamlMember(Description = "Enable new CSP weather handling. Allows rain and smooth weather transitions. Requires CSP 0.1.76+")]
    public bool EnableWeatherFx { get; init; } = false;
    [YamlMember(Description = "Lock server date to real date. This stops server time \"running away\" when using a high time multiplier, so that in-game sunrise/sunset times are based on the current date")]
    public bool LockServerDate { get; set; } = true;
    [YamlMember(Description = "Reduce track grip when the track is wet. This is much worse than proper CSP rain physics but allows you to run clients with public/Patreon CSP at the same time")]
    public double RainTrackGripReductionPercent { get; set; } = 0;
    [YamlMember(Description = "Enable AI traffic")]
    public bool EnableAi { get; init; } = false;
    [YamlMember(Description = "Override the country shown in CM. Please do not use this unless the autodetected country is wrong", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public List<string>? GeoParamsCountryOverride { get; init; } = null;
    [YamlMember(Description = "List of plugins to enable")]
    public List<string> EnablePlugins { get; init; } = [];
    [YamlMember(Description = "Ignore some common configuration errors. More info: https://assettoserver.org/docs/common-configuration-errors")]
    public IgnoreConfigurationErrors IgnoreConfigurationErrors { get; init; } = new();
    [YamlMember(Description = "Enable CSP client messages feature. Requires CSP 0.1.77+")]
    public bool EnableClientMessages { get; init; } = false;
    [YamlMember(Description = "Enable CSP UDP client messages feature. Required for VR head/hand syncing. Requires CSP 0.1.80+")]
    public bool EnableUdpClientMessages { get; init; } = false;
    [YamlMember(Description = "Log unknown CSP Lua client messages / online events")]
    public bool DebugClientMessages { get; set; } = false;
    [YamlMember(Description = "Enable CSP custom position updates. This is an improved version of batched position updates, reducing network traffic even further. CSP 0.1.77+ required")]
    public bool EnableCustomUpdate { get; set; } = false;
    [YamlMember(Description = "Maximum time a player can spend on the loading screen before being disconnected")]
    public int PlayerLoadingTimeoutMinutes { get; set; } = 10;
    [YamlMember(Description = "Maximum time the server will wait for a checksum response before disconnecting the player")]
    public int PlayerChecksumTimeoutSeconds { get; set; } = 40;
    [YamlMember(Description = "Send logs to a Loki instance, e.g. Grafana Cloud", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public LokiSettings? LokiSettings { get; init; }
    [YamlMember(Description = "Port to control the server using Source RCON protocol. 0 to disable.")]
    public ushort RconPort { get; init; } = 0;
    [YamlMember(Description = "Dump contents of welcome message and CSP extra options to a file. For debug purposes only.")]
    public bool DebugWelcomeMessage { get; init; } = false;
    [YamlMember(Description = "Force clients to use track params (coordinates, time zone) specified on the server. CSP 0.1.79+ required")]
    public bool ForceServerTrackParams { get; init; } = false;
    [YamlMember(Description = "Allow cars to have multiple data checksums. Instead of a single checksummed data.acd, you can have multiple data*.acd files in the car folder and players can join with any of these files")]
    public bool EnableAlternativeCarChecksums { get; init; } = false;
    [YamlMember(Description = "Enable the AC UDP plugin interface compatible with Kunos acServer plugins")]
    public bool EnableLegacyPluginInterface { get; init; } = false;
    [YamlMember(Description = "Automatically configure port forwards using UPnP or NAT-PMP. Empty = Enable on Windows when lobby registration is enabled. true = Always enable, detailed error log. false = Always disable")]
    public bool? EnableUPnP { get; init; }
    [YamlMember(Description = "List of URLs for custom loading screen images. A random image will be picked from this list. Requires CSP 0.2.0+ and a recent version of Content Manager")]
    public List<string>? LoadingImageUrls { get; set; }
    [YamlMember(Description = "Name and path of file-based user groups")]
    public Dictionary<string, string> UserGroups { get; init; } = new()
    {
        { "default_blacklist", "blacklist.txt" },
        { "default_whitelist", "whitelist.txt" },
        { "default_admins", "admins.txt" }
    };
    [YamlMember(Description = "Name of user group to be used for blacklist")]
    public string BlacklistUserGroup { get; init; } = "default_blacklist";
    [YamlMember(Description = "Name of user group to be used for whitelist")]
    public string WhitelistUserGroup { get; init; } = "default_whitelist";
    [YamlMember(Description = "Name of user group to be used for admins")]
    public string AdminUserGroup { get; init; } = "default_admins";
    [YamlMember(Description = "List of allowed origins for Cross-Origin Resource Sharing. Use this if you want to query this server from a website")]
    public List<string>? CorsAllowedOrigins { get; init; }
    [YamlMember(Description = "Allow a user group to execute specific admin commands")]
    public List<UserGroupCommandPermissions>? UserGroupCommandPermissions { get; init; }
    
    public AiParams AiParams { get; init; } = new();

    [YamlIgnore] public int MaxAfkTimeMilliseconds => MaxAfkTimeMinutes * 60_000;
    [YamlIgnore] public string Path { get; private set; } = null!;

    public void ToFile(string path)
    {
        using var stream = File.CreateText(path);
        var serializer = new SerializerBuilder().Build();
        serializer.Serialize(stream, this);
    }
        
    public static ACExtraConfiguration FromFile(string path)
    {
        using var stream = File.OpenText(path);

        var deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .Build();

        var yamlParser = new Parser(stream);
        yamlParser.Consume<StreamStart>();
        yamlParser.Accept<DocumentStart>(out _);

        var extraCfg = deserializer.Deserialize<ACExtraConfiguration>(yamlParser);

        extraCfg.Path = path;
        return extraCfg;
    }

    internal void LoadPluginConfig(ACPluginLoader loader, ContainerBuilder builder)
    {
        using var stream = File.OpenText(Path);

        var yamlParser = new Parser(stream);
        yamlParser.Consume<StreamStart>();
        yamlParser.Accept<DocumentStart>(out _);
        yamlParser.Accept<DocumentStart>(out _);
        
        var deserializerBuilder = new DeserializerBuilder().WithoutNodeTypeResolver(typeof(PreventUnknownTagsNodeTypeResolver));
        foreach (var plugin in loader.LoadedPlugins)
        {
            if (plugin.ConfigurationType != null)
            {
                deserializerBuilder.WithTagMapping("!" + plugin.ConfigurationType.Name, plugin.ConfigurationType);
            }
        }

        var deserializer = deserializerBuilder.Build();

        while (yamlParser.Accept<DocumentStart>(out _))
        {
            var pluginConfig = deserializer.Deserialize(yamlParser)!;

            foreach (var plugin in loader.LoadedPlugins)
            {
                if (plugin.ConfigurationType == pluginConfig.GetType() && plugin.ValidatorType != null)
                {
                    var validator = Activator.CreateInstance(plugin.ValidatorType)!;
                    var method = typeof(DefaultValidatorExtensions).GetMethod("ValidateAndThrow")!;
                    var generic = method.MakeGenericMethod(pluginConfig.GetType());
                    try
                    {
                        generic.Invoke(null, new[] { validator, pluginConfig });
                    }
                    catch (TargetInvocationException ex)
                    {
                        throw ex.InnerException ?? ex;
                    }

                    break;
                }
            }
            
            builder.RegisterInstance(pluginConfig).AsSelf();
        }
    }
}

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class IgnoreConfigurationErrors
{
    public bool MissingCarChecksums { get; init; }
    public bool MissingTrackParams { get; init; }
    public bool WrongServerDetails { get; init; }
    public bool UnsafeAdminWhitelist { get; init; }
}

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class LokiSettings
{
    public string? Url { get; init; }
    public string? Login { get; init; }
    public string? Password { get; init; }
}

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public partial class AiParams : ObservableObject
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
    [YamlMember(Description = "Minimum spawn distance to players")]
    public float SpawnSafetyDistanceToPlayerMeters { get; set; } = 150;
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
    
    [ObservableProperty]
    [property: YamlMember(Description = "Default AI car deceleration for obstacle/collision detection (m/s^2)")]
    private float _defaultDeceleration = 8.5f;
    
    [ObservableProperty]
    [property: YamlMember(Description = "Default AI car acceleration for obstacle/collision detection (m/s^2)")]
    private float _defaultAcceleration = 2.5f;
    
    [ObservableProperty]
    [property: YamlMember(Description = "Maximum AI car target count for AI slot overbooking. This is not an absolute maximum and might be slightly higher")]
    private int _maxAiTargetCount = 300;
    
    [ObservableProperty]
    [property: YamlMember(Description = "Number of AI cars per player the server will try to keep")]
    private int _aiPerPlayerTargetCount = 10;
    
    [YamlMember(Description = "Soft player limit, the server will stop accepting new players when this many players are reached. Use this to ensure a minimum amount of AI cars. 0 to disable.")]
    public int MaxPlayerCount { get; set; } = 0;
    [YamlMember(Description = "Hide AI car nametags and make them invisible on the minimap. Broken on CSP versions < 0.1.78")]
    public bool HideAiCars { get; set; } = false;
    
    [ObservableProperty]
    [property: YamlMember(Description = "AI spline height offset. Use this if the AI spline is too close to the ground")]
    private float _splineHeightOffsetMeters = 0;
    
    [YamlMember(Description = "Lane width for adjacent lane detection")]
    public float LaneWidthMeters { get; init; } = 3.0f;
    [YamlMember(Description = "Enable two way traffic. This will allow AI cars to spawn in lanes with the opposite direction of travel to the player.")]
    public bool TwoWayTraffic { get; set; } = false;
    [YamlMember(Description = "Enable traffic spawning if the player is driving the wrong way. Only takes effect when TwoWayTraffic is set to false.")]
    public bool WrongWayTraffic { get; set; } = true;
    
    [ObservableProperty]
    [property: YamlMember(Description = "AI cornering speed factor. Lower = AI cars will drive slower around corners.")]
    private float _corneringSpeedFactor = 0.65f;
    
    [ObservableProperty]
    [property: YamlMember(Description = "AI cornering brake distance factor. Lower = AI cars will brake later for corners.")]
    private float _corneringBrakeDistanceFactor = 3;
    
    [ObservableProperty]
    [property: YamlMember(Description = "AI cornering brake force factor. This is multiplied with DefaultDeceleration. Lower = AI cars will brake less hard for corners.")]
    private float _corneringBrakeForceFactor = 0.5f;
    
    [YamlMember(Description = "Name prefix for AI cars. Names will be in the form of '<NamePrefix> <SessionId>'")]
    public string NamePrefix { get; init; } = "Traffic";
    [YamlMember(Description = "Ignore obstacles for some time if the AI car is stopped for longer than x seconds")]
    public int IgnoreObstaclesAfterSeconds { get; set; } = 10;
    
    [ObservableProperty]
    [property: YamlMember(Description = "Apply scale to some traffic density related settings. Increasing this DOES NOT magically increase your traffic density, it is dependent on your other settings. Values higher than 1 not recommended.")]
    private float _trafficDensity = 1.0f;
    
    [YamlMember(Description = "Dynamic (hourly) traffic density. List must have exactly 24 entries in the format [0.2, 0.5, 1, 0.7, ...]")]
    public List<float>? HourlyTrafficDensity { get; set; }
    
    [ObservableProperty]
    [property: YamlMember(Description = "Tyre diameter of AI cars in meters, shouldn't have to be changed unless some cars are creating lots of smoke.")]
    private float _tyreDiameterMeters = 0.65f;
    
    [YamlMember(Description = "Apply some smoothing to AI spline camber")]
    public bool SmoothCamber { get; init; } = false;
    [YamlMember(Description = "Show debug overlay for AI cars")]
    public bool Debug { get; set; } = false;
    [YamlMember(Description = "Update interval for AI spawn point finder")]
    public int AiBehaviorUpdateIntervalHz { get; set; } = 2;
    [YamlMember(Description = "AI cars inside these areas will ignore all player obstacles")]
    public List<Sphere>? IgnorePlayerObstacleSpheres { get; set; }
    [YamlMember(Description = "Override some settings for newly spawned cars based on the number of lanes")]
    public Dictionary<int, LaneCountSpecificOverrides> LaneCountSpecificOverrides { get; set; } = new();

    [YamlMember(Description = "Override some settings for specific car models")]
    public List<CarSpecificOverrides> CarSpecificOverrides { get; init; } = [
        new CarSpecificOverrides
        {
            Model = "my_car_model",
            Acceleration = 2.5f,
            Deceleration = 8.5f,
            AllowedLanes = new List<LaneSpawnBehavior> { LaneSpawnBehavior.Left, LaneSpawnBehavior.Middle, LaneSpawnBehavior.Right },
            MaxOverbooking = 1,
            CorneringSpeedFactor = 0.5f,
            CorneringBrakeDistanceFactor = 3,
            CorneringBrakeForceFactor = 0.5f,
            EngineIdleRpm = 800,
            EngineMaxRpm = 3000,
            MaxLaneCount = 2,
            MinLaneCount = 1,
            TyreDiameterMeters = 0.8f,
            SplineHeightOffsetMeters = 0,
            VehicleLengthPostMeters = 2,
            VehicleLengthPreMeters = 2,
            MinAiSafetyDistanceMeters = 20,
            MaxAiSafetyDistanceMeters = 25,
            MinCollisionStopTimeSeconds = 0,
            MaxCollisionStopTimeSeconds = 0,
            MinSpawnProtectionTimeSeconds = 30,
            MaxSpawnProtectionTimeSeconds = 60
        }
    ];

    [YamlIgnore] public float PlayerRadiusSquared => PlayerRadiusMeters * PlayerRadiusMeters;
    [YamlIgnore] public float PlayerAfkTimeoutMilliseconds => PlayerAfkTimeoutSeconds * 1000;
    [YamlIgnore] public float MaxPlayerDistanceToAiSplineSquared => MaxPlayerDistanceToAiSplineMeters * MaxPlayerDistanceToAiSplineMeters;
    [YamlIgnore] public int MinAiSafetyDistanceSquared => MinAiSafetyDistanceMeters * MinAiSafetyDistanceMeters;
    [YamlIgnore] public int MaxAiSafetyDistanceSquared => MaxAiSafetyDistanceMeters * MaxAiSafetyDistanceMeters;
    [YamlIgnore] public float StateSpawnDistanceSquared => StateSpawnDistanceMeters * StateSpawnDistanceMeters;
    [YamlIgnore] public float MinStateDistanceSquared => MinStateDistanceMeters * MinStateDistanceMeters;
    [YamlIgnore] public float SpawnSafetyDistanceToPlayerSquared => SpawnSafetyDistanceToPlayerMeters * SpawnSafetyDistanceToPlayerMeters;
    [YamlIgnore] public int MinSpawnProtectionTimeMilliseconds => MinSpawnProtectionTimeSeconds * 1000;
    [YamlIgnore] public int MaxSpawnProtectionTimeMilliseconds => MaxSpawnProtectionTimeSeconds * 1000;
    [YamlIgnore] public int MinCollisionStopTimeMilliseconds => MinCollisionStopTimeSeconds * 1000;
    [YamlIgnore] public int MaxCollisionStopTimeMilliseconds => MaxCollisionStopTimeSeconds * 1000;
    [YamlIgnore] public float MaxSpeedMs => MaxSpeedKph / 3.6f;
    [YamlIgnore] public float RightLaneOffsetMs => RightLaneOffsetKph / 3.6f;
    [YamlIgnore] public int IgnoreObstaclesAfterMilliseconds => IgnoreObstaclesAfterSeconds * 1000;
    [YamlIgnore] public int AiBehaviorUpdateIntervalMilliseconds => 1000 / AiBehaviorUpdateIntervalHz;
}

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

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class LaneCountSpecificOverrides
{
    [YamlMember(Description = "Minimum distance between AI cars")]
    public int MinAiSafetyDistanceMeters { get; set; }
    [YamlMember(Description = "Maximum distance between AI cars")]
    public int MaxAiSafetyDistanceMeters { get; set; }
    [YamlIgnore] public int MinAiSafetyDistanceSquared => MinAiSafetyDistanceMeters * MinAiSafetyDistanceMeters;
    [YamlIgnore] public int MaxAiSafetyDistanceSquared => MaxAiSafetyDistanceMeters * MaxAiSafetyDistanceMeters;
}

public enum AfkKickBehavior
{
    PlayerInput,
    MinimumSpeed
}

public enum LaneSpawnBehavior
{
    Left,
    Middle,
    Right
}

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class Sphere
{
    public Vector3 Center { get; set; }
    public float RadiusMeters { get; set; }
}

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class UserGroupCommandPermissions
{
    public required string UserGroup { get; set; }
    public required List<string> Commands { get; set; }
}
