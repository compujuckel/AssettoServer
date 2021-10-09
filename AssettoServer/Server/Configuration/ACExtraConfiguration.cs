using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssettoServer.Server.Configuration
{
    public class ACExtraConfiguration
    {
        public bool UseSteamAuth { get; set; } = false;
        public bool EnableAntiAfk { get; set; } = true;
        public int MaxAfkTimeMinutes { get; set; } = 10;
        public int MaxPing { get; set; } = 500;
        public int MaxPingSeconds { get; set; } = 10;
        public bool ForceLights { get; set; }
        public float NetworkBubbleDistance { get; set; } = 500;
        public int OutsideNetworkBubbleRefreshRateHz { get; set; } = 4;
        public bool EnableServerDetails { get; set; } = true;
        public string ServerDescription { get; set; } = "";
        public string OwmApiKey { get; set; } = "";
        public bool EnableLiveWeather { get; set; } = false;
        public bool EnableRealTime { get; set; } = false;
        public bool EnableWeatherFx { get; set; } = false;
        public double RainTrackGripReduction { get; set; } = 0;
        public bool EnableAi { get; set; } = false;

        public AiParams AiParams { get; set; } = new AiParams();

        [JsonIgnore]
        public int MaxAfkTimeMilliseconds => MaxAfkTimeMinutes * 60000;
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
        public int StateSafetyDistance { get; set; } = 1500;
        public float SpawnSafetyDistanceToPlayer { get; set; } = 80;
        public int MinSpawnProtectionTime { get; set; } = 4;
        public int MaxSpawnProtectionTime { get; set; } = 8;
        public float AiSplineHeightOffset { get; set; }
        public float MaxSpeed { get; set; } = 80;
        public float MaxSpeedVariation { get; set; } = 0.15f;
        public float DefaultDeceleration { get; set; } = -8.5f;
        public float DefaultAcceleration { get; set; } = 2.5f;

        public int MaxAiTargetCount { get; set; } = 300;
        public int AiPerPlayerTargetCount { get; set; } = 10;
        public int MaxPlayerCount { get; set; } = 0;

        [JsonIgnore] public float PlayerAfkTimeoutMilliseconds => PlayerAfkTimeout * 1000;
        [JsonIgnore] public float MaxPlayerDistanceToAiSplineSquared => MaxPlayerDistanceToAiSpline * MaxPlayerDistanceToAiSpline;
        [JsonIgnore] public int MinAiSafetyDistanceSquared => MinAiSafetyDistance * MinAiSafetyDistance;
        [JsonIgnore] public int MaxAiSafetyDistanceSquared => MaxAiSafetyDistance * MaxAiSafetyDistance;
        [JsonIgnore] public int StateSafetyDistanceSquared => StateSafetyDistance * StateSafetyDistance;
        [JsonIgnore] public float SpawnSafetyDistanceToPlayerSquared => SpawnSafetyDistanceToPlayer * SpawnSafetyDistanceToPlayer;
        [JsonIgnore] public int MinSpawnProtectionTimeMilliseconds => MinSpawnProtectionTime * 1000;
        [JsonIgnore] public int MaxSpawnProtectionTimeMilliseconds => MaxSpawnProtectionTime * 1000;
        [JsonIgnore] public float MaxSpeedMs => MaxSpeed / 3.6f;
    }
}
