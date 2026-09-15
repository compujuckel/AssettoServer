using System;

namespace AssettoServer.Utils;

public static class RandomExtensions
{
    extension(Random self)
    {
        public float NextSingle(float minValue, float maxValue)
        {
            return self.NextSingle() * (maxValue - minValue) + minValue;
        }

        public double NextDouble(double minValue, double maxValue)
        {
            return self.NextDouble() * (maxValue - minValue) + minValue;
        }
    }
}
