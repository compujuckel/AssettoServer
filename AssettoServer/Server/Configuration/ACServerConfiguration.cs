using System;
using IniParser;
using IniParser.Model;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AssettoServer.Server.Ai;
using AssettoServer.Server.Plugin;
using AssettoServer.Server.Weather;
using Serilog;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NodeTypeResolvers;

namespace AssettoServer.Server.Configuration
{
    public class ACServerConfiguration
    {
        public string Name { get; internal set; }
        public string Password { get; internal set; }
        public string AdminPassword { get; internal set; }
        public int MaxClients { get; internal set; }

        public ushort UdpPort { get; internal set; }
        public ushort TcpPort { get; internal set; }
        public int HttpPort { get; internal set; }

        public byte RefreshRateHz { get; internal set; }

        public IReadOnlyList<EntryCar> EntryCars { get; internal set; }
        public IReadOnlyList<SessionConfiguration> Sessions { get; internal set; }
        public IReadOnlyList<WeatherConfiguration> Weathers { get; internal set; }

        public string Track { get; internal set; }
        public string TrackConfig { get; internal set; }
        public string FullTrackName { get; internal set; }
        public float SunAngle { get; internal set; }
        public int MaxBallastKg { get; internal set; }
        public int QualifyMaxWaitPercentage { get; internal set; }
        public byte TractionControlAllowed { get; internal set; }
        public byte ABSAllowed { get; internal set; }
        public short AllowedTyresOutCount { get; internal set; }
        public bool AllowTyreBlankets { get; internal set; }
        public bool AutoClutchAllowed { get; internal set; }
        public float FuelConsumptionRate { get; internal set; }
        public bool HasExtraLap { get; internal set; }
        public short InvertedGridPositions { get; internal set; }
        public bool IsGasPenaltyDisabled { get; internal set; }
        public bool IsVirtualMirrorForced { get; internal set; }
        public bool RegisterToLobby { get; internal set; }
        public byte JumpStartPenaltyMode { get; internal set; }
        public float MechanicalDamageRate { get; internal set; }
        public short PitWindowStart { get; internal set; }
        public short PitWindowEnd { get; internal set; }
        public bool StabilityAllowed { get; internal set; }
        public int RaceOverTime { get; internal set; }
        public int ResultScreenTime { get; internal set; }
        public float TyreConsumptionRate { get; internal set; }
        public byte MaxContactsPerKm { get; internal set; }
        public float TrackGrip { get; internal set; }
        public string LegalTyres { get; internal set; }
        public string WelcomeMessage { get; internal set; }
        public float TimeOfDayMultiplier { get; internal set; }
        public ACExtraConfiguration Extra { get; internal set; }
        public CMContentConfiguration ContentConfiguration { get; internal set; }
        public DynamicTrackConfiguration DynamicTrack { get; internal set; } = new DynamicTrackConfiguration();
        public string ServerVersion { get; internal set; }
        public string CSPExtraOptions { get; internal set; }

        public ACServerConfiguration FromFiles(string preset, string serverCfgPath, string entryListPath, ACPluginLoader loader)
        {
            string configBaseFolder = string.IsNullOrEmpty(preset) ? "cfg" : Path.Join("presets", preset);
            if (string.IsNullOrEmpty(serverCfgPath))
            {
                serverCfgPath = Path.Join(configBaseFolder, "server_cfg.ini");
            }

            if (string.IsNullOrEmpty(entryListPath))
            {
                entryListPath = Path.Join(configBaseFolder, "entry_list.ini");
            }

            var parser = new FileIniDataParser();
            IniData data = parser.ReadFile(serverCfgPath);
            var server = data["SERVER"];
            Name = server["NAME"];
            Track = server["TRACK"];
            TrackConfig = server["CONFIG_TRACK"];
            FullTrackName = string.IsNullOrEmpty(TrackConfig) ? Track : Track + "-" + TrackConfig;
            Password = server["PASSWORD"];
            AdminPassword = server["ADMIN_PASSWORD"];
            UdpPort = ushort.Parse(server["UDP_PORT"]);
            TcpPort = ushort.Parse(server["TCP_PORT"]);
            HttpPort = int.Parse(server["HTTP_PORT"]);
            MaxBallastKg = int.Parse(server["MAX_BALLAST_KG"]);
            RefreshRateHz = byte.Parse(server["CLIENT_SEND_INTERVAL_HZ"]);
            MaxClients = int.Parse(server["MAX_CLIENTS"]);
            ABSAllowed = byte.Parse(server["ABS_ALLOWED"]);
            TractionControlAllowed = byte.Parse(server["TC_ALLOWED"]);
            AllowedTyresOutCount = short.Parse(server["ALLOWED_TYRES_OUT"]);
            AllowTyreBlankets = server["TYRE_BLANKETS_ALLOWED"] != "0";
            AutoClutchAllowed = server["AUTOCLUTCH_ALLOWED"] != "0";
            FuelConsumptionRate = float.Parse(server["FUEL_RATE"]) / 100;
            HasExtraLap = server["RACE_EXTRA_LAP"] != "0";
            InvertedGridPositions = short.Parse(server["REVERSED_GRID_RACE_POSITIONS"]);
            IsGasPenaltyDisabled = server["RACE_GAS_PENALTY_DISABLED"] != "0";
            IsVirtualMirrorForced = server["FORCE_VIRTUAL_MIRROR"] != "0";
            RegisterToLobby = server["REGISTER_TO_LOBBY"] != "0";
            JumpStartPenaltyMode = byte.Parse(server["START_RULE"]);
            MechanicalDamageRate = float.Parse(server["DAMAGE_MULTIPLIER"]) / 100;
            PitWindowStart = short.Parse(server["RACE_PIT_WINDOW_START"] ?? "0");
            PitWindowEnd = short.Parse(server["RACE_PIT_WINDOW_END"] ?? "0");
            StabilityAllowed = server["STABILITY_ALLOWED"] != "0";
            RaceOverTime = int.Parse(server["RACE_OVER_TIME"]);
            ResultScreenTime = int.Parse(server["RESULT_SCREEN_TIME"]);
            SunAngle = float.Parse(server["SUN_ANGLE"]);
            TyreConsumptionRate = float.Parse(server["TYRE_WEAR_RATE"]) / 100;
            byte.TryParse(server["MAX_CONTACTS_PER_KM"], out byte maxContactsPerKm);
            MaxContactsPerKm = maxContactsPerKm;
            LegalTyres = server["LEGAL_TYRES"];
            TimeOfDayMultiplier = float.Parse(server["TIME_OF_DAY_MULT"]);

            var dynTrack = data["DYNAMIC_TRACK"];
            if(dynTrack.Count > 0)
            {
                DynamicTrack.Enabled = true;
                DynamicTrack.BaseGrip = float.Parse(dynTrack["SESSION_START"]) / 100;
                DynamicTrack.GripPerLap = float.Parse(dynTrack["SESSION_TRANSFER"]) / 100;
                DynamicTrack.TotalLapCount = float.Parse(dynTrack["LAP_GAIN"]);
            }

            string extraCfgPath = Path.Join(configBaseFolder, "extra_cfg.yml");
            ACExtraConfiguration extraCfg;
            if (File.Exists(extraCfgPath))
            {
                using var stream = File.OpenText(extraCfgPath);

                var deserializer = new DeserializerBuilder().Build();

                var yamlParser = new Parser(stream);
                yamlParser.Consume<StreamStart>();
                yamlParser.Accept<DocumentStart>(out _);

                extraCfg = deserializer.Deserialize<ACExtraConfiguration>(yamlParser);

                foreach (var pluginName in extraCfg.EnablePlugins)
                {
                    loader.LoadPlugin(pluginName);
                }
                
                var deserializerBuilder = new DeserializerBuilder().WithoutNodeTypeResolver(typeof(PreventUnknownTagsNodeTypeResolver));
                foreach (var plugin in loader.LoadedPlugins)
                {
                    if (plugin.ConfigurationType != null)
                    {
                        deserializerBuilder.WithTagMapping("!" + plugin.ConfigurationType.Name, plugin.ConfigurationType);
                    }
                }
                deserializer = deserializerBuilder.Build();

                while (yamlParser.Accept<DocumentStart>(out _))
                {
                    var pluginConfig = deserializer.Deserialize(yamlParser);
                    loader.LoadConfiguration(pluginConfig);
                }
            }
            else
            {
                using var stream = File.CreateText(extraCfgPath);
                var serializer = new SerializerBuilder().Build();
                extraCfg = new ACExtraConfiguration();
                serializer.Serialize(stream, extraCfg);
            }

            Extra = extraCfg;

            if (Regex.IsMatch(Name, @"x:\w+$"))
            {
                const string errorMsg =
                    "Server details are configured via ID in server name. This interferes with native AssettoServer server details. More info: https://github.com/compujuckel/AssettoServer/wiki/Common-configuration-errors#wrong-server-details";
                if (Extra.IgnoreConfigurationErrors.WrongServerDetails)
                {
                    Log.Warning(errorMsg);
                }
                else
                {
                    throw new ConfigurationException(errorMsg);
                }
            }

            if (Extra.RainTrackGripReductionPercent is < 0 or > 1)
            {
                throw new ConfigurationException("RainTrackGripReductionPercent must be in the range 0..1");
            }
            if (Extra.AiParams.MaxSpeedVariationPercent is < 0 or > 1)
            {
                throw new ConfigurationException("MaxSpeedVariationPercent must be in the range 0..1");
            }

            if(Extra.EnableServerDetails)
            {
                string cmContentPath = Path.Join(configBaseFolder, "cm_content/content.json");
                CMContentConfiguration cmContent = new CMContentConfiguration();
                // Only load if the file already exists, otherwise this will fail if the content directory does not exist
                if (File.Exists(cmContentPath))
                {
                    cmContent = JsonConvert.DeserializeObject<CMContentConfiguration>(File.ReadAllText(cmContentPath));

                    File.WriteAllText(cmContentPath, JsonConvert.SerializeObject(cmContent, Formatting.Indented));

                    ContentConfiguration = cmContent;
                }
            }

            string welcomeMessagePath = string.IsNullOrEmpty(preset) ? server["WELCOME_MESSAGE"] : Path.Join(configBaseFolder, server["WELCOME_MESSAGE"]);
            if (File.Exists(welcomeMessagePath))
            {
                WelcomeMessage = File.ReadAllText(welcomeMessagePath);
            }
            else if(!string.IsNullOrEmpty(welcomeMessagePath))
            {
                Log.Warning("Welcome message not found at {0}", Path.GetFullPath(welcomeMessagePath));
            }

            string cspExtraOptionsPath = Path.Join(configBaseFolder, "csp_extra_options.ini"); 
            if (File.Exists(cspExtraOptionsPath))
            {
                CSPExtraOptions = File.ReadAllText(cspExtraOptionsPath);
            }

            List<WeatherConfiguration> weathers = new List<WeatherConfiguration>();
            for(int i = 0; ; i++)
            {
                var weather = data["WEATHER_" + i];
                if (weather.Count == 0)
                    break;

                var weatherConfiguration = new WeatherConfiguration
                {
                    Graphics = weather["GRAPHICS"],
                    BaseTemperatureAmbient = float.Parse(weather["BASE_TEMPERATURE_AMBIENT"]),
                    BaseTemperatureRoad = float.Parse(weather["BASE_TEMPERATURE_ROAD"]),
                    VariationAmbient = float.Parse(weather["VARIATION_AMBIENT"]),
                    VariationRoad = float.Parse(weather["VARIATION_ROAD"]),
                    WindBaseSpeedMin = float.Parse(weather["WIND_BASE_SPEED_MIN"]),
                    WindBaseSpeedMax = float.Parse(weather["WIND_BASE_SPEED_MAX"]),
                    WindBaseDirection = int.Parse(weather["WIND_BASE_DIRECTION"]),
                    WindVariationDirection = int.Parse(weather["WIND_VARIATION_DIRECTION"]),
                    WeatherFxParams = WeatherFxParams.FromString(weather["GRAPHICS"])
                };

                weathers.Add(weatherConfiguration);
            }

            Weathers = weathers;

            List<SessionConfiguration> sessions = new List<SessionConfiguration>();

            var practiceConfig = data["PRACTICE"];
            if (practiceConfig.Count > 0)
                sessions.Add(new SessionConfiguration
                {
                    Id = 0,
                    Name = "Practice",
                    IsOpen = practiceConfig["IS_OPEN"] == "1",
                    Time = int.Parse(practiceConfig["TIME"]),
                    WaitTime = int.Parse(practiceConfig["WAIT_TIME"] ?? "0")
                });

            var qualityConfig = data["QUALIFY"];
            if (qualityConfig.Count > 0)
                sessions.Add(new SessionConfiguration
                {
                    Id = 1,
                    Name = "Qualify",
                    IsOpen = qualityConfig["IS_OPEN"] == "1",
                    Time = int.Parse(qualityConfig["TIME"]),
                    WaitTime = int.Parse(qualityConfig["WAIT_TIME"] ?? "0")
                });

            var raceConfig = data["RACE"];
            if (raceConfig.Count > 0)
                sessions.Add(new SessionConfiguration
                {
                    Id = 2,
                    Name = "Race",
                    IsOpen = raceConfig["IS_OPEN"] == "1",
                    Laps = int.Parse(raceConfig["LAPS"]),
                    WaitTime = int.Parse(raceConfig["WAIT_TIME"] ?? "0")
                });

            Sessions = sessions;

            var entryCarsParser = new FileIniDataParser();
            IniData entryData = entryCarsParser.ReadFile(entryListPath);

            List<EntryCar> entryCars = new List<EntryCar>();
            for(int i = 0; i < MaxClients; i++)
            {
                var entry = entryData["CAR_" + i];
                if (entry.Count == 0)
                    break;

                AiMode aiMode = AiMode.Disabled;
                if (Extra.EnableAi)
                {
                    Enum.TryParse(entry["AI"], true, out aiMode);
                }

                var driverOptions = CSPDriverOptions.Parse(entry["SKIN"]);

                entryCars.Add(new EntryCar
                {
                    Model = entry["MODEL"],
                    Skin = entry["SKIN"],
                    SpectatorMode = int.Parse(entry["SPECTATOR_MODE"] ?? "0"),
                    Ballast = int.Parse(entry["BALLAST"]),
                    Restrictor = int.Parse(entry["RESTRICTOR"]),
                    DriverOptionsFlags = driverOptions,
                    AiMode = aiMode,
                    AiEnableColorChanges = driverOptions.HasFlag(DriverOptionsFlags.AllowColorChange),
                    AiControlled = aiMode != AiMode.Disabled,
                    AiPakSequenceIds = new byte[MaxClients],
                    LastSeenAiState = new AiState[MaxClients],
                    LastSeenAiSpawn = new byte[MaxClients],
                    AiSplineHeightOffsetMeters = Extra.AiParams.SplineHeightOffsetMeters
                });
            }

            EntryCars = entryCars;
            
            foreach (var carOverrides in Extra.AiParams.CarSpecificOverrides)
            {
                var matchedCars = EntryCars.Where(c => c.Model == carOverrides.Model).ToList();
                foreach (var car in matchedCars)
                {
                    if(carOverrides.EnableColorChanges.HasValue)
                        car.AiEnableColorChanges = carOverrides.EnableColorChanges.Value;
                    if (carOverrides.SplineHeightOffsetMeters.HasValue)
                        car.AiSplineHeightOffsetMeters = carOverrides.SplineHeightOffsetMeters.Value;
                    if (carOverrides.EngineIdleRpm.HasValue)
                        car.AiIdleEngineRpm = carOverrides.EngineIdleRpm.Value;
                    if (carOverrides.EngineMaxRpm.HasValue)
                        car.AiMaxEngineRpm = carOverrides.EngineMaxRpm.Value;
                }
                
                foreach (var skinOverrides in carOverrides.SkinSpecificOverrides)
                {
                    foreach (var car in matchedCars.Where(c => c.Skin == skinOverrides.Skin))
                    {
                        if(skinOverrides.EnableColorChanges.HasValue)
                            car.AiEnableColorChanges = skinOverrides.EnableColorChanges.Value;
                    }
                }
            }

            return this;
        }
    }
}
