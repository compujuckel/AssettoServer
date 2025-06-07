using System.Collections.Generic;
using System.IO;
using System.Numerics;
using AssettoServer.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using JetBrains.Annotations;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

#pragma warning disable CS0657

namespace AssettoServer.Server.Configuration.Extra;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public partial class ACExtraConfiguration : ObservableObject
{
    [YamlMember(Description = "Override minimum CSP version required to join this server. Leave this empty to not require CSP.")]
    public uint? MinimumCSPVersion { get; init; } = CSPVersion.V0_1_77;
    [YamlMember(Description = "Enable Steam ticket validation. Requires CSP 0.1.75+ and a recent version of Content Manager")]
    public bool UseSteamAuth { get; init; } = false;
    [YamlMember(Description = "Enable generation of Guid from name instead of SteamID. Required for ACPro", DefaultValuesHandling = DefaultValuesHandling.OmitDefaults)]
    public bool EnableACProSupport { get; init; } = false;
    [YamlMember(Description = "Steam Web API key for Steam authentication. You only need this on platforms that don't support Steam natively (e.g. ARM64)", DefaultValuesHandling = DefaultValuesHandling.OmitDefaults)]
    public string? SteamWebApiKey { get; init; }
    [YamlMember(Description = "List of DLC App IDs that are required to join. Steam auth must be enabled. Possible values: https://steamdb.info/app/244210/dlc/")]
    public List<int> ValidateDlcOwnership { get; init; } = [];
    [YamlMember(Description = "Enable protection against cheats/hacks. 0 = No protection. 1 = Block all public cheats as of 2023-11-18 (ClientSecurityPlugin and CSP 0.2.0+ required)")]
    public int MandatoryClientSecurityLevel { get; internal set; }
    [YamlMember(Description = "Force headlights on for all cars")]
    public bool ForceLights { get; set; }
    [YamlMember(Description = "Enable usage of /resetcar to teleport the player to the closest spline point. Requires CSP v0.2.8 (3424) or later")]
    public bool EnableCarReset { get; set; } = false;
    [YamlMember(Description = "Enable vanilla server voting for: Session skip; Session restart")]
    public bool EnableSessionVote { get; set; } = true;
    [YamlMember(Description = "Enable vanilla server voting to kick a player")]
    public bool EnableKickPlayerVote { get; set; } = true;
    [YamlMember(Description = "Minimum number of connected players for session and kick voting to work. Default is 5")]
    public ushort VoteKickMinimumConnectedPlayers { get; set; } = 3;
    [YamlMember(Description = "Enable global usage of DRS. Recommended to disable for Qualification and Race sessions")]
    public bool EnableGlobalDrs { get; set; } = true;
    [YamlMember(Description = "Enable unlimited usage of Push-to-Pass. Recommended to disable for Qualification and Race sessions")]
    public bool EnableUnlimitedP2P { get; set; } = true;
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
    public bool EnableWeatherFx { get; init; } = true;
    [YamlMember(Description = "Lock server date to real date. This stops server time \"running away\" when using a high time multiplier, so that in-game sunrise/sunset times are based on the current date")]
    public bool LockServerDate { get; set; } = true;
    [YamlMember(Description = "Reduce track grip when the track is wet. This is much worse than proper CSP rain physics but allows you to run clients with public/Patreon CSP at the same time")]
    public double RainTrackGripReductionPercent { get; set; } = 0;
    [YamlMember(Description = "Enable AI traffic")]
    public bool EnableAi { get; init; } = false;
    [YamlMember(Description = "Override the country shown in CM. Please do not use this unless the autodetected country is wrong", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public List<string>? GeoParamsCountryOverride { get; init; } = null;
    [YamlMember(Description = "List of plugins to enable")]
    public List<string>? EnablePlugins { get; init; }
    [YamlMember(Description = "Ignore some common configuration errors. More info: https://assettoserver.org/docs/common-configuration-errors")]
    public IgnoreConfigurationErrors IgnoreConfigurationErrors { get; init; } = new();
    [YamlMember(Description = "Enable CSP client messages feature. Requires CSP 0.1.77+")]
    public bool EnableClientMessages { get; init; } = true;
    [YamlMember(Description = "Enable CSP UDP client messages feature. Required for VR head/hand syncing. Requires CSP 0.2.0+")]
    public bool EnableUdpClientMessages { get; init; } = false;
    [YamlMember(Description = "Log unknown CSP Lua client messages / online events", DefaultValuesHandling = DefaultValuesHandling.OmitDefaults)]
    public bool DebugClientMessages { get; set; } = false;
    [YamlMember(Description = "Enable CSP custom position updates. This is an improved version of batched position updates, reducing network traffic even further. CSP 0.1.77+ required")]
    public bool EnableCustomUpdate { get; set; } = true;
    [YamlMember(Description = "Maximum time a player can spend on the loading screen before being disconnected")]
    public int PlayerLoadingTimeoutMinutes { get; set; } = 10;
    [YamlMember(Description = "Maximum time the server will wait for a checksum response before disconnecting the player")]
    public int PlayerChecksumTimeoutSeconds { get; set; } = 40;
    [YamlMember(Description = "Send logs to a Loki instance, e.g. Grafana Cloud", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public LokiSettings? LokiSettings { get; init; }
    [YamlMember(Description = "Port to control the server using Source RCON protocol. 0 to disable.")]
    public ushort RconPort { get; init; } = 0;
    [YamlMember(Description = "Dump contents of welcome message and CSP extra options to a file. For debug purposes only.", DefaultValuesHandling = DefaultValuesHandling.OmitDefaults)]
    public bool DebugWelcomeMessage { get; init; } = false;
    [YamlMember(Description = "Server scripts for this user group will be loaded locally and script checksums disabled. For debug purposes only.", DefaultValuesHandling = DefaultValuesHandling.OmitDefaults)]
    public string? DebugScriptUserGroup { get; init; }
    [YamlMember(Description = "Force clients to use track params (coordinates, time zone) specified on the server. CSP 0.1.79+ required")]
    public bool ForceServerTrackParams { get; init; } = false;
    [YamlMember(Description = "Allow cars to have multiple data checksums. Instead of a single checksummed data.acd, you can have multiple data*.acd files in the car folder and players can join with any of these files")]
    public bool EnableAlternativeCarChecksums { get; init; } = false;
    [YamlMember(Description = "Enable the AC UDP plugin interface compatible with Kunos acServer plugins")]
    public bool EnableLegacyPluginInterface { get; init; } = false;
    [YamlMember(Description = "Automatically configure port forwards using UPnP or NAT-PMP. Empty = Enable on Windows when lobby registration is enabled. true = Always enable, detailed error log. false = Always disable", DefaultValuesHandling = DefaultValuesHandling.OmitDefaults)]
    public bool? EnableUPnP { get; init; }
    [YamlMember(Description = "List of URLs for custom loading screen images. A random image will be picked from this list. Requires CSP 0.2.0+ and a recent version of Content Manager")]
    public List<string>? LoadingImageUrls { get; set; }
    [YamlMember(Description = "Anonymize player IP addresses in outputs")]
    public bool RedactIpAddresses { get; init; } = true;
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
    [YamlMember(Description = "List of allowed origins for Cross-Origin Resource Sharing. Use this if you want to query this server from a website", DefaultValuesHandling = DefaultValuesHandling.OmitDefaults)]
    public List<string>? CorsAllowedOrigins { get; init; }
    [YamlMember(Description = "Allow a user group to execute specific admin commands")]
    public List<UserGroupCommandPermissions>? UserGroupCommandPermissions { get; init; }
    
    public AiParams AiParams { get; init; } = new();
    
    [YamlIgnore] internal bool ContainsObsoletePluginConfiguration { get; private set; }

    public void ToFile(string path)
    {
        using var writer = File.CreateText(path);
        ToStream(writer);
    }

    public void ToStream(StreamWriter writer)
    {
        var builder = new SerializerBuilder();
        builder.Build().Serialize(writer, this);
    }
        
    public static ACExtraConfiguration FromFile(string path)
    {
        using var stream = File.OpenText(path);

        var deserializer = new DeserializerBuilder().Build();
        
        var yamlParser = new Parser(stream);
        yamlParser.Consume<StreamStart>();
        yamlParser.Accept<DocumentStart>(out _);

        var extraCfg = deserializer.Deserialize<ACExtraConfiguration>(yamlParser);

        if (yamlParser.Accept<DocumentStart>(out _))
        {
            extraCfg.ContainsObsoletePluginConfiguration = true;
        }
        
        return extraCfg;
    }

    public static readonly ACExtraConfiguration ReferenceConfiguration = new()
    {
        LokiSettings = new LokiSettings
        {
            Url = "http://localhost",
            Login = "username",
            Password = "password"
        },
        UserGroupCommandPermissions = [
            new UserGroupCommandPermissions
            {
                UserGroup = "weather",
                Commands = [
                    "setweather",
                    "setcspweather",
                    "setrain"
                ]
            }
        ],
        AiParams = new AiParams
        {
            CarSpecificOverrides = [
                new CarSpecificOverrides
                {
                    Model = "my_car_model",
                    Acceleration = 2.5f,
                    Deceleration = 8.5f,
                    AllowedLanes = [LaneSpawnBehavior.Left, LaneSpawnBehavior.Middle, LaneSpawnBehavior.Right],
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
            ],
            LaneCountSpecificOverrides = new Dictionary<int, LaneCountSpecificOverrides>
            {
                {
                    1,
                    new LaneCountSpecificOverrides
                    {
                        MinAiSafetyDistanceMeters = 50,
                        MaxAiSafetyDistanceMeters = 100
                    }
                },
                {
                    2,
                    new LaneCountSpecificOverrides
                    {
                        MinAiSafetyDistanceMeters = 40,
                        MaxAiSafetyDistanceMeters = 80
                    }
                }
            },
            IgnorePlayerObstacleSpheres = [
                new Sphere
                {
                    Center = new Vector3(0, 0, 0),
                    RadiusMeters = 50
                }
            ]
        }
    };
}
