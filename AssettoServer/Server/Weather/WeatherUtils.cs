using System;

namespace AssettoServer.Server.Weather;

public static class WeatherUtils
{
    private const int TimeMinimum = 8 * 60 * 60;
    private const int TimeMaximum = 18 * 60 * 60;
        
    // From https://github.com/gro-ove/actools/blob/master/AcTools/Processes/Game.Properties.cs#L481
    public static double GetRoadTemperature(double seconds, double ambientTemperature, double weatherCoefficient = 1.0)
    {
        if (seconds < TimeMinimum || seconds > TimeMaximum)
        {
            var minTemperature = GetRoadTemperature(TimeMinimum, ambientTemperature, weatherCoefficient);
            var maxTemperature = GetRoadTemperature(TimeMaximum, ambientTemperature, weatherCoefficient);
            var minValue = TimeMinimum;
            var maxValue = TimeMaximum - 24 * 60 * 60;
            if (seconds > TimeMaximum)
            {
                seconds -= 24 * 60 * 60;
            }

            return minTemperature + (maxTemperature - minTemperature) * (seconds - minValue) / (maxValue - minValue);
        }

        var time = (seconds / 60d / 60d - 7d) * 0.04167;
        return ambientTemperature * (1d + 5.33332 * (weatherCoefficient == 0d ? 1d : weatherCoefficient) * (1d - time) *
            (Math.Exp(-6d * time) * Math.Sin(6d * time) + 0.25) * Math.Sin(0.9 * time));
    }

    public static double SecondsFromSunAngle(double sunAngle)
    {
        return sunAngle * (50400.0 - 46800.0) / 16.0 + 46800.0;
    }

    public static double SunAngleFromSeconds(double seconds)
    {
        return 16.0 * (seconds - 46800.0) / (50400.0 - 46800.0);
    }

    public static double SunAngleFromTicks(long ticks)
    {
        return SunAngleFromSeconds(ticks / 10_000_000.0);
    }
}
