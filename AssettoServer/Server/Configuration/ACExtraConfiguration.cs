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
        public float AiSplineHeightOffset { get; set; }

        [JsonIgnore]
        public int MaxAfkTimeMilliseconds => MaxAfkTimeMinutes * 60000;
    }
}
