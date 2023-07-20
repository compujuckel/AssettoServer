using Prometheus;

namespace AssettoServer.Utils;

public static class MetricDefaults
{
    public static readonly SummaryConfiguration DefaultQuantiles = new SummaryConfiguration
    {
        Objectives = new[]
        {
            new QuantileEpsilonPair(0.5, 0.05),
            new QuantileEpsilonPair(0.75, 0.05),
            new QuantileEpsilonPair(0.95, 0.01),
            new QuantileEpsilonPair(0.99, 0.005),
        }
    };
}
