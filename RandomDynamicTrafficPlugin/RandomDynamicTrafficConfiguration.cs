using YamlDotNet.Serialization;

namespace RandomDynamicTrafficPlugin
{
    public class RandomDynamicTrafficConfiguration
    {
        public string Hello { get; init; } = "World!";
        [YamlMember(Description = "Is random dynamic traffic enabled?")]
        public bool IsEnabled { get; set; } = false;

        [YamlMember(Description = "Density of the minimum traffic density when using random dynamic traffic")]
        public float MinTrafficDensity { get; set; } = 0.0f;

        [YamlMember(Description = "Density of the maximum traffic density when using random dynamic traffic")]
        public float MaxTrafficDensity { get; set; } = 2.5f;

        [YamlMember(Description = "Density of the maximum traffic density when using random dynamic traffic")]
        public int MinAdjustmentsSeconds { get; set; } = 30;

        [YamlMember(Description = "Density of the maximum traffic density when using random dynamic traffic")]
        public int MaxAdjustmentsSeconds { get; set; } = 60;

        [YamlMember(Description = "Density of traffic and lower that qualifies Low traffic")]
        public float LowTrafficDensity { get; set; } = 0.4f;

        [YamlMember(Description = "Density of traffic and lower that qualifies Casual traffic")]
        public float CasualTrafficDensity { get; set; } = 0.8f;

        [YamlMember(Description = "Density of traffic and lower that qualifies Peak traffic")]
        public float PeakTrafficDensity { get; set; } = 1.5f;
        [YamlMember(Description = "The speed which is used to calculate the average speed adjustment")]
        public float MiddlePointSpeed { get; set; } = 150f;
        [YamlMember(Description = "Maximum amount of adjustment to the density using speed adjustments")]
        public float MaxSpeedDensityAdjustment { get; set; } = 0.3f;

    }
}
