using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using AssettoServer.Server.Plugin;
using Autofac;
using FluentValidation;
using JetBrains.Annotations;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NodeTypeResolvers;

namespace AssettoServer.Server.Configuration;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class ACExtraConfiguration
{
    [YamlMember(Description = "Enable Steam ticket validation. Requires CSP 0.1.75+ and a recent version of Content Manager")]
    public bool UseSteamAuth { get; init; } = false;
    [YamlMember(Description = "List of DLC App IDs that are required to join. Steam auth must be enabled. Possible values: https://steamdb.info/app/244210/dlc/")]
    public List<int> ValidateDlcOwnership { get; init; } = new();
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
    [YamlMember(Description = "Reduce track grip when the track is wet. This is much worse than proper CSP rain physics but allows you to run clients with public/Patreon CSP at the same time")]
    public double RainTrackGripReductionPercent { get; set; } = 0;
    [YamlMember(Description = "Enable AI traffic")]
    public bool EnableAi { get; init; } = false;
    [YamlMember(Description = "Override the country shown in CM. Please do not use this unless the autodetected country is wrong", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public List<string>? GeoParamsCountryOverride { get; init; } = null;
    [YamlMember(Description = "List of plugins to enable")]
    public List<string> EnablePlugins { get; init; } = new();
    [YamlMember(Description = "Ignore some common configuration errors. More info: https://assettoserver.org/docs/common-configuration-errors")]
    public IgnoreConfigurationErrors IgnoreConfigurationErrors { get; init; } = new();
    [YamlMember(Description = "Enable CSP client messages feature. Requires CSP 0.1.77+")]
    public bool EnableClientMessages { get; init; } = false;
    [YamlMember(Description = "Enable CSP custom position updates. This is an improved version of batched position updates, reducing network traffic even further. CSP 0.1.77+ required")]
    public bool EnableCustomUpdate { get; set; } = false;
    [YamlMember(Description = "Maximum time a player can spend on the loading screen before being disconnected")]
    public int PlayerLoadingTimeoutMinutes { get; set; } = 10;
    [YamlMember(Description = "Send logs to a Loki instance, e.g. Grafana Cloud", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public LokiSettings? LokiSettings { get; init; }
    [YamlMember(Description = "Port to control the server using Source RCON protocol. 0 to disable.")]
    public ushort RconPort { get; init; } = 0;
    [YamlMember(Description = "Dump contents of welcome message and CSP extra options to a file. For debug purposes only.")]
    public bool DebugWelcomeMessage { get; init; } = false;
    [YamlMember(Description = "Force clients to use track params (coordinates, time zone) specified on the server. CSP 0.1.79+ required")]
    public bool ForceServerTrackParams = false;
    [YamlMember(Description = "Allow cars to have multiple data checksums. Instead of a single checksummed data.acd, you can have multiple data*.acd files in the car folder and players can join with any of these files")]
    public bool EnableAlternativeCarChecksums = false;
    [YamlMember(Description = "Enable the AC UDP plugin interface compatible with Kunos acServer plugins")]
    public bool EnableLegacyPluginInterface = false;
    [YamlMember(Description = "Automatically configure port forwards using UPnP or NAT-PMP. Empty = Enable on Windows when lobby registration is enabled. true = Always enable, detailed error log. false = Always disable")]
    public bool? EnableUPnP;
    [YamlMember(Description = "Name and path of file-based user groups")]
    public Dictionary<string, string> UserGroups { get; init; } = new()
    {
        { "default_blacklist", "blacklist.txt" },
        { "default_whitelist", "whitelist.txt" },
        { "default_admins", "admins.txt" }
    };
    [YamlMember(Description = "Name of user group to be used for blacklist")]
    public string BlacklistUserGroup { get; set; } = "default_blacklist";
    [YamlMember(Description = "Name of user group to be used for whitelist")]
    public string WhitelistUserGroup { get; set; } = "default_whitelist";
    [YamlMember(Description = "Name of user group to be used for admins")]
    public string AdminUserGroup { get; set; } = "default_admins";
    
    public AiParams AiParams { get; init; } = new AiParams();

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
    [YamlMember(Description = "Hide AI car nametags and make them invisible on the minimap. Broken on CSP versions < 0.1.78")]
    public bool HideAiCars { get; set; } = false;
    [YamlMember(Description = "AI spline height offset. Use this if the AI spline is too close to the ground")]
    public float SplineHeightOffsetMeters { get; set; } = 0;
    [YamlMember(Description = "Lane width for adjacent lane detection")]
    public float LaneWidthMeters { get; init; } = 3.0f;
    [YamlMember(Description = "Enable two way traffic. This will allow AI cars to spawn in lanes with the opposite direction of travel to the player.")]
    public bool TwoWayTraffic { get; set; } = false;
    [YamlMember(Description = "AI cornering speed factor. Lower = AI cars will drive slower around corners.")]
    public float CorneringSpeedFactor { get; set; } = 1;
    [YamlMember(Description = "AI cornering brake distance factor. Lower = AI cars will brake later for corners.")]
    public float CorneringBrakeDistanceFactor { get; set; } = 1;
    [YamlMember(Description = "AI cornering brake force factor. This is multiplied with DefaultDeceleration. Lower = AI cars will brake less hard for corners.")]
    public float CorneringBrakeForceFactor { get; set; } = 1;
    [YamlMember(Description = "Name prefix for AI cars. Names will be in the form of '<NamePrefix> <SessionId>'")]
    public string NamePrefix { get; init; } = "Traffic";
    [YamlMember(Description = "Ignore obstacles for some time if the AI car is stopped for longer than x seconds")]
    public int IgnoreObstaclesAfterSeconds { get; set; } = 10;
    [YamlMember(Description = "Apply scale to some traffic density related settings. Increasing this DOES NOT magically increase your traffic density, it is dependent on your other settings. Values higher than 1 not recommended.")]
    public float TrafficDensity { get; set; } = 1.0f;
    [YamlMember(Description = "Dynamic (hourly) traffic density. List must have exactly 24 entries in the format [0.2, 0.5, 1, 0.7, ...]")]
    public List<float>? HourlyTrafficDensity { get; set; }
    [YamlMember(Description = "Tyre diameter of AI cars in meters, shouldn't have to be changed unless some cars are creating lots of smoke.")]
    public float TyreDiameterMeters { get; set; } = 0.65f;
    [YamlMember(Description = "Apply some smoothing to AI spline camber")]
    public bool SmoothCamber { get; init; } = false;
    [YamlMember(Description = "Show debug overlay for AI cars")]
    public bool Debug { get; set; } = false;
    [YamlMember(Description = "Update interval for AI spawn point finder")]
    public int AiBehaviorUpdateIntervalHz { get; set; } = 2;
    [YamlMember(Description = "Override some settings for newly spawned cars based on the number of lanes")]
    public Dictionary<int, LaneCountSpecificOverrides> LaneCountSpecificOverrides { get; set; } = new();
    [YamlMember(Description = "Override some settings for specific car models/skins")]
    public List<CarSpecificOverrides> CarSpecificOverrides { get; init; } = new();

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
    [YamlMember(Description = "Allow random color changes after respawn")]
    public bool? EnableColorChanges { get; init; }
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

    [YamlMember(Description = "Override some settings for specific skins of this car model")]
    public List<SkinSpecificOverrides> SkinSpecificOverrides { get; init; } = new();
}

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class SkinSpecificOverrides
{
    [YamlMember(Description = "Skin to match for these overrides")]
    public string? Skin { get; init; }
    [YamlMember(Description = "Allow random color changes after respawn")]
    public bool? EnableColorChanges { get; init; }
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
