using System.Collections.Generic;
using AssettoServer.Utils;
using IniParser;
using IniParser.Model;
using JetBrains.Annotations;

namespace AssettoServer.Server.Configuration.Kunos;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class ServerConfiguration
{
    [IniField("SERVER", "NAME")] public string Name { get; set; } = "AssettoServer";
    [IniField("SERVER", "PASSWORD")] public string? Password { get; set; }
    [IniField("SERVER", "ADMIN_PASSWORD")] public string? AdminPassword { get; set; }
    [IniField("SERVER", "MAX_CLIENTS")] public int MaxClients { get; internal set; }
    [IniField("SERVER", "UDP_PORT")] public ushort UdpPort { get; set; } = 9600;
    [IniField("SERVER", "TCP_PORT")] public ushort TcpPort { get; set; } = 9600;
    [IniField("SERVER", "HTTP_PORT")] public ushort HttpPort { get; set; } = 8081;
    [IniField("SERVER", "CLIENT_SEND_INTERVAL_HZ")] public byte RefreshRateHz { get; init; } = 20;
    [IniField("SERVER", "TRACK")] public string Track { get; internal set; } = "";
    [IniField("SERVER", "CONFIG_TRACK")] public string TrackConfig { get; init; } = "";
    [IniField("SERVER", "SUN_ANGLE")] public float SunAngle { get; init; }
    [IniField("SERVER", "LOOP_MODE")] public bool Loop { get; init; } = true;
    [IniField("SERVER", "TC_ALLOWED")] public byte TractionControlAllowed { get; init; }
    [IniField("SERVER", "ABS_ALLOWED")] public byte ABSAllowed { get; init; }
    [IniField("SERVER", "ALLOWED_TYRES_OUT")] public short AllowedTyresOutCount { get; init; }
    [IniField("SERVER", "TYRE_BLANKETS_ALLOWED")] public bool AllowTyreBlankets { get; init; }
    [IniField("SERVER", "AUTOCLUTCH_ALLOWED")] public bool AutoClutchAllowed { get; init; }
    [IniField("SERVER", "FUEL_RATE", Percent = true)] public float FuelConsumptionRate { get; init; }
    [IniField("SERVER", "RACE_EXTRA_LAP")] public bool HasExtraLap { get; set; }
    [IniField("SERVER", "QUALIFY_MAX_WAIT_PERC")] public ushort QualifyMaxWait { get; init; } = 120;
    [IniField("SERVER", "REVERSED_GRID_RACE_POSITIONS")] public short InvertedGridPositions { get; init; }
    [IniField("SERVER", "RACE_GAS_PENALTY_DISABLED")] public bool IsGasPenaltyDisabled { get; init; }
    [IniField("SERVER", "FORCE_VIRTUAL_MIRROR")] public bool IsVirtualMirrorForced { get; init; }
    [IniField("SERVER", "REGISTER_TO_LOBBY")] public bool RegisterToLobby { get; init; }
    [IniField("SERVER", "START_RULE")] public byte JumpStartPenaltyMode { get; init; }
    [IniField("SERVER", "DAMAGE_MULTIPLIER", Percent = true)] public float MechanicalDamageRate { get; init; }
    [IniField("SERVER", "RACE_PIT_WINDOW_START")] public short PitWindowStart { get; init; }
    [IniField("SERVER", "RACE_PIT_WINDOW_END")] public short PitWindowEnd { get; init; }
    [IniField("SERVER", "STABILITY_ALLOWED")] public bool StabilityAllowed { get; init; }
    [IniField("SERVER", "RACE_OVER_TIME")] public int RaceOverTime { get; init; }
    [IniField("SERVER", "RESULT_SCREEN_TIME")] public int ResultScreenTime { get; init; }
    [IniField("SERVER", "TYRE_WEAR_RATE", Percent = true)] public float TyreConsumptionRate { get; init; }
    [IniField("SERVER", "MAX_CONTACTS_PER_KM", IgnoreParsingErrors = true)] public byte MaxContactsPerKm { get; init; }
    [IniField("SERVER", "LEGAL_TYRES")] public string LegalTyres { get; init; } = "";
    [IniField("SERVER", "WELCOME_MESSAGE")] public string WelcomeMessagePath { get; init; } = "";
    [IniField("SERVER", "TIME_OF_DAY_MULT")] public float TimeOfDayMultiplier { get; set; }
    [IniField("SERVER", "UDP_PLUGIN_ADDRESS")] public string? UdpPluginAddress { get; set; }
    [IniField("SERVER", "UDP_PLUGIN_LOCAL_PORT")] public ushort UdpPluginLocalPort { get; set; }
    [IniField("SERVER", "KICK_QUORUM")] public ushort KickQuorum { get; set; } = 80;
    [IniField("SERVER", "VOTING_QUORUM")] public ushort VotingQuorum { get; set; } = 70;
    [IniField("SERVER", "VOTE_DURATION")] public ushort VoteDuration { get; set; } = 20;

    [IniSection("WEATHER")] public IReadOnlyList<WeatherConfiguration> Weathers { get; init; } = new List<WeatherConfiguration>();
    [IniSection("DYNAMIC_TRACK")] public DynamicTrackConfiguration DynamicTrack { get; init; } = new();
    [IniSection("BOOK")] public SessionConfiguration? Booking { get; init; }
    [IniSection("PRACTICE")] public SessionConfiguration? Practice { get; init; }
    [IniSection("QUALIFY")] public SessionConfiguration? Qualify { get; init; }
    [IniSection("RACE")] public SessionConfiguration? Race { get; init; }

    public static ServerConfiguration FromFile(string path)
    {
        var parser = new FileIniDataParser();
        IniData data = parser.ReadFile(path);
        return data.DeserializeObject<ServerConfiguration>();
    }

    public bool CheckAdminPassword(string password)
    {
        return !string.IsNullOrWhiteSpace(AdminPassword) && AdminPassword == password;
    }
}
