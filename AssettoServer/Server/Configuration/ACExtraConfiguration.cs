using System.Collections.Generic;
using AssettoServer.Server.Weather;
using YamlDotNet.Serialization;

namespace AssettoServer.Server.Configuration
{
    public class ACExtraConfiguration
    {
        public bool UseSteamAuth { get; set; } = false;
        public bool EnableAntiAfk { get; set; } = true;
        public int MaxAfkTimeMinutes { get; set; } = 10;
        public AfkKickBehavior AfkKickBehavior { get; set; } = AfkKickBehavior.PlayerInput;
        public int MaxPing { get; set; } = 500;
        public int MaxPingSeconds { get; set; } = 10;
        public bool ForceLights { get; set; }
        public float NetworkBubbleDistance { get; set; } = 500;
        public int OutsideNetworkBubbleRefreshRateHz { get; set; } = 4;
        public bool EnableServerDetails { get; set; } = true;
        public string ServerDescription { get; set; } = "";
        public string OwmApiKey { get; set; } = "";
        public bool EnableLiveWeather { get; set; } = false;
        public bool EnableWeatherVoting { get; set; } = false;
        public List<WeatherFxType> VotingBlacklistedWeathers { get; set; } = new List<WeatherFxType>() { WeatherFxType.None };
        public bool EnableRealTime { get; set; } = false;
        public bool EnableWeatherFx { get; set; } = false;
        public int WeatherUpdateIntervalMinutes { get; set; } = 10;
        public double RainTrackGripReduction { get; set; } = 0;
        public bool EnableAi { get; set; } = false;
        public AiParams AiParams { get; set; } = new AiParams();
        public List<string> NameFilters { get; set; } = new();
        public List<string> EnablePlugins { get; set; } = new();

        [YamlIgnore] public int MaxAfkTimeMilliseconds => MaxAfkTimeMinutes * 60_000;
        [YamlIgnore] public int WeatherUpdateIntervalMilliseconds => WeatherUpdateIntervalMinutes * 60_000;
    }

    public class AiParams
    {
        public float PlayerRadius { get; set; } = 200.0f;
        public float PlayerPositionOffset { get; set; } = 100.0f;
        public long PlayerAfkTimeout { get; set; } = 10;
        public float MaxPlayerDistanceToAiSpline { get; set; } = 7;
        public int MinSpawnDistance { get; set; } = 100;
        public int MaxSpawnDistance { get; set; } = 400;
        public int MinAiSafetyDistance { get; set; } = 20;
        public int MaxAiSafetyDistance { get; set; } = 70;
        public float StateSpawnDistance { get; set; } = 1000;
        public float MinStateDistance { get; set; } = 200;
        public float StateTieBreakerDistance { get; set; } = 250;
        public float SpawnSafetyDistanceToPlayer { get; set; } = 100;
        public int MinSpawnProtectionTime { get; set; } = 4;
        public int MaxSpawnProtectionTime { get; set; } = 8;
        public int MinCollisionStopTime { get; set; } = 1;
        public int MaxCollisionStopTime { get; set; } = 3;
        public float AiSplineHeightOffset { get; set; }
        public float MaxSpeed { get; set; } = 80;
        public float RightLaneOffset { get; set; } = 10;
        public float MaxSpeedVariation { get; set; } = 0.15f;
        public float DefaultDeceleration { get; set; } = -8.5f;
        public float DefaultAcceleration { get; set; } = 2.5f;
        public int MaxAiTargetCount { get; set; } = 300;
        public int AiPerPlayerTargetCount { get; set; } = 10;
        public int MaxPlayerCount { get; set; } = 0;
        public bool HideAiCars { get; set; } = false;
        public float SplineHeightOffset { get; set; } = 0;
        public float LaneWidth { get; set; } = 3.0f;
        public bool TwoWayTraffic { get; set; } = false;

        [YamlIgnore] public float PlayerRadiusSquared => PlayerRadius * PlayerRadius;
        [YamlIgnore] public float PlayerAfkTimeoutMilliseconds => PlayerAfkTimeout * 1000;
        [YamlIgnore] public float MaxPlayerDistanceToAiSplineSquared => MaxPlayerDistanceToAiSpline * MaxPlayerDistanceToAiSpline;
        [YamlIgnore] public int MinAiSafetyDistanceSquared => MinAiSafetyDistance * MinAiSafetyDistance;
        [YamlIgnore] public int MaxAiSafetyDistanceSquared => MaxAiSafetyDistance * MaxAiSafetyDistance;
        [YamlIgnore] public float StateSpawnDistanceSquared => StateSpawnDistance * StateSpawnDistance;
        [YamlIgnore] public float MinStateDistanceSquared => MinStateDistance * MinStateDistance;
        [YamlIgnore] public float StateTieBreakerDistanceSquared => StateTieBreakerDistance * StateTieBreakerDistance;
        [YamlIgnore] public float SpawnSafetyDistanceToPlayerSquared => SpawnSafetyDistanceToPlayer * SpawnSafetyDistanceToPlayer;
        [YamlIgnore] public int MinSpawnProtectionTimeMilliseconds => MinSpawnProtectionTime * 1000;
        [YamlIgnore] public int MaxSpawnProtectionTimeMilliseconds => MaxSpawnProtectionTime * 1000;
        [YamlIgnore] public int MinCollisionStopTimeMilliseconds => MinCollisionStopTime * 1000;
        [YamlIgnore] public int MaxCollisionStopTimeMilliseconds => MaxCollisionStopTime * 1000;
        [YamlIgnore] public float MaxSpeedMs => MaxSpeed / 3.6f;
        [YamlIgnore] public float RightLaneOffsetMs => RightLaneOffset / 3.6f;
    }

    public enum AfkKickBehavior
    {
        PlayerInput,
        MinimumSpeed
    }
}
