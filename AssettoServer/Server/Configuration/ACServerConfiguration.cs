using IniParser;
using IniParser.Model;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using AssettoServer.Server.Ai;
using AssettoServer.Server.Weather;

namespace AssettoServer.Server.Configuration
{
    public class ACServerConfiguration
    {
        public string Name { get; internal set; }
        public string Password { get; internal set; }
        public string AdminPassword { get; internal set; }
        public int MaxClients { get; internal set; }

        public int UdpPort { get; internal set; }
        public int TcpPort { get; internal set; }
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

        public ACServerConfiguration FromFiles()
        {
            var parser = new FileIniDataParser();
            IniData data = parser.ReadFile("cfg/server_cfg.ini");
            var server = data["SERVER"];
            Name = server["NAME"];
            Track = server["TRACK"];
            TrackConfig = server["CONFIG_TRACK"];
            FullTrackName = string.IsNullOrEmpty(TrackConfig) ? Track : Track + "-" + TrackConfig;
            Password = server["PASSWORD"];
            AdminPassword = server["ADMIN_PASSWORD"];
            UdpPort = int.Parse(server["UDP_PORT"]);
            TcpPort = int.Parse(server["TCP_PORT"]);
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

            string extraCfgPath = "cfg/extra_cfg.json";
            ACExtraConfiguration extraCfg = new ACExtraConfiguration();
            if (File.Exists(extraCfgPath))
                extraCfg = JsonConvert.DeserializeObject<ACExtraConfiguration>(File.ReadAllText(extraCfgPath));

            File.WriteAllText(extraCfgPath, JsonConvert.SerializeObject(extraCfg, Formatting.Indented));

            Extra = extraCfg;

            if(Extra.EnableServerDetails)
            {
                Name = Name + " ℹ" + HttpPort;

                string cmContentPath = "cfg/cm_content/content.json";
                CMContentConfiguration cmContent = new CMContentConfiguration();
                // Only load if the file already exists, otherwise this will fail if the content directory does not exist
                if (File.Exists(cmContentPath))
                {
                    cmContent = JsonConvert.DeserializeObject<CMContentConfiguration>(File.ReadAllText(cmContentPath));

                    File.WriteAllText(cmContentPath, JsonConvert.SerializeObject(cmContent, Formatting.Indented));

                    ContentConfiguration = cmContent;
                }
            }

            string welcomeMessagePath = server["WELCOME_MESSAGE"];
            if (File.Exists(welcomeMessagePath))
                WelcomeMessage = File.ReadAllText(welcomeMessagePath);

            List<WeatherConfiguration> weathers = new List<WeatherConfiguration>();
            for(int i = 0; ; i++)
            {
                var weather = data["WEATHER_" + i];
                if (weather.Count == 0)
                    break;

                var weatherConfiguration = new WeatherConfiguration
                {
                    Graphics = weather["GRAPHICS"],
                    BaseTemperatureAmbient = int.Parse(weather["BASE_TEMPERATURE_AMBIENT"]),
                    BaseTemperatureRoad = int.Parse(weather["BASE_TEMPERATURE_ROAD"]),
                    VariationAmbient = int.Parse(weather["VARIATION_AMBIENT"]),
                    VariationRoad = int.Parse(weather["VARIATION_ROAD"]),
                    WindBaseSpeedMin = int.Parse(weather["WIND_BASE_SPEED_MIN"]),
                    WindBaseSpeedMax = int.Parse(weather["WIND_BASE_SPEED_MAX"]),
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
            IniData entryData = entryCarsParser.ReadFile("cfg/entry_list.ini");

            List<EntryCar> entryCars = new List<EntryCar>();
            for(int i = 0; i < MaxClients; i++)
            {
                var entry = entryData["CAR_" + i];
                if (entry.Count == 0)
                    break;

                if (entry["AI"] == "1")
                {
                    entryCars.Add(new AiCar()
                    {
                        Model = entry["MODEL"],
                        Skin = entry["SKIN"],
                        SpectatorMode = int.Parse(entry["SPECTATOR_MODE"] ?? "0"),
                        Ballast = int.Parse(entry["BALLAST"]),
                        Restrictor = int.Parse(entry["RESTRICTOR"])
                    });
                }
                else
                {
                    entryCars.Add(new EntryCar
                    {
                        Model = entry["MODEL"],
                        Skin = entry["SKIN"],
                        SpectatorMode = int.Parse(entry["SPECTATOR_MODE"] ?? "0"),
                        Ballast = int.Parse(entry["BALLAST"]),
                        Restrictor = int.Parse(entry["RESTRICTOR"])
                    });
                }
            }

            EntryCars = entryCars;

            return this;
        }
    }
}
