using System;

namespace AssettoServer.Utils;

public static class MathUtils
{
    public static double Smoothstep(double fromValue, double toValue, double by)
    {
        double x = Math.Clamp((by - fromValue) / (toValue - fromValue), 0, 1);

        return x * x * (3 - 2 * x);
    }

    public static double Lerp(double fromValue, double toValue, double by)
    {
        return fromValue * (1 - by) + toValue * by;
    }
}
