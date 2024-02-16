using System;

namespace AssettoServer.Utils;

public static class RandomExtensions
{
    public static float NextSingle(this Random self, float minValue, float maxValue)
    {
        return self.NextSingle() * (maxValue - minValue) + minValue;
    }
    
    public static double NextDouble(this Random self, double minValue, double maxValue)
    {
        return self.NextDouble() * (maxValue - minValue) + minValue;
    }
}
