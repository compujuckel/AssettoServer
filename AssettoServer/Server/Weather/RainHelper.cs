using System;

namespace AssettoServer.Server.Weather
{
    // Calculations based on Sol 2.2 alpha 7 by Peter Boese
    public class RainHelper
    {
        private const double WetnessDryingAirtempK = 1.0;
        private const double WetnessDryingAirtempMin = 8.0;
        private const double WetnessDryingRoadtempK = 1.0;
        private const double WetnessDryingRoadtempMin = 12.0;
        private const double WetnessDryingSunK = 3.0;
        private const double WetnessRainWettingK = 75.0;
        private const double WetnessHumidityWettingK = 1.0;
        private const double PuddlesDryingK = 1.0;
        private const double PuddlesRainWettingK = 6.0;
        private const double PuddlesConstantDrain = 0.25;
        private const double TimeK = 0.001;

        private double _tempWetness;

        private static double Smoothstep(double fromValue, double toValue, double by)
        {
            double x = Math.Clamp((by - fromValue) / (toValue - fromValue), 0, 1);

            return x * x * (3 - 2 * x);
        }

        private static double Lerp(double fromValue, double toValue, double by)
        {
            return fromValue * (1 - by) + toValue * by;
        }

        private static double GetTemperatureOffset(double tempBase, double mult, double lowLimit, double highLimit, double temp)
        {
            double f = Math.Pow(Math.Abs(temp - tempBase), mult);
            double result;

            if (temp < tempBase)
            {
                f = Math.Abs(temp - tempBase) / tempBase;
                result = 1.0 - (f * mult);
            }
            else if (temp > tempBase)
            {
                f = Math.Abs(temp - tempBase) / (40 - tempBase);
                result = 1.0 + (f * mult);
            }
            else
            {
                result = 1.0;
            }

            return Math.Clamp(result, lowLimit, highLimit);
        }

        private static double TempInterpol(double temp, double tempBase, double value1, double value2)
        {
            double tmp = GetTemperatureOffset(tempBase, 1.0f, 0, 2, temp);
            tmp = tmp * 0.5;

            return Lerp(value1, value2, tmp);
        }

        private void CalcWater(WeatherData condition, double sun, double dt)
        {
            double dryingForce = Math.Max(0, WetnessDryingAirtempK * TempInterpol(condition.TemperatureAmbient, WetnessDryingAirtempMin, -1, 2)) +
                                 Math.Max(0, WetnessDryingRoadtempK * TempInterpol(condition.TemperatureRoad, WetnessDryingRoadtempMin, -1, 2)) + 
                                 (WetnessDryingSunK * sun); // TODO add sun angle

            double tempWetting = dt * TimeK * (-dryingForce + (WetnessRainWettingK * Math.Pow(condition.RainIntensity, 1.7)) +
                                               Math.Max(0, WetnessHumidityWettingK * condition.Humidity * TempInterpol(condition.TemperatureAmbient, WetnessDryingAirtempMin, 1, -1)));

            _tempWetness += tempWetting;

            double tempPuddleDrain = PuddlesConstantDrain * TempInterpol(condition.TemperatureRoad, WetnessDryingRoadtempMin, 0, 3);

            if (tempWetting > 0)
            {
                if (_tempWetness >= 1.0)
                {
                    condition.RainWater =
                        (float) Math.Clamp(
                            condition.RainWater + dt * TimeK * PuddlesRainWettingK * (_tempWetness - 1) * condition.RainIntensity *
                            Math.Clamp(Math.Pow(1.0 * Math.Max(0, condition.RainIntensity - condition.RainWater), 3 - 2 * Math.Pow(condition.RainIntensity, 4)), 0, 1), 0, 1);
                }

                condition.RainWater = (float) Math.Clamp(condition.RainWater - dt * TimeK * tempPuddleDrain, 0, 1);
            }
            else if (tempWetting < 0)
            {
                condition.RainWater = (float) Math.Clamp(condition.RainWater + dt * TimeK * (PuddlesDryingK * (_tempWetness - 1) - tempPuddleDrain), 0, 1);
            }

            condition.RainWetness = (float) Math.Clamp(_tempWetness + Math.Min(0.1, condition.RainWater * 5), 0, 1);
            _tempWetness = Math.Clamp(_tempWetness, 0, 3);

            if (tempWetting < 0 && _tempWetness >= 1)
            {
                _tempWetness = 1;
            }
        }

        private void CalcGrip(WeatherData condition, double baseGrip, double rainTrackGripReduction)
        {
            condition.TrackGrip = (float) (baseGrip - Lerp(0, rainTrackGripReduction * 0.4, condition.RainWetness) - Lerp(0, rainTrackGripReduction * 0.6, condition.RainWater));
        }

        public void Update(WeatherData weather, double baseGrip, double rainTrackGripReduction, long dt)
        {
            if (weather.Type.WeatherFxType != weather.UpcomingType.WeatherFxType)
            {
                weather.TransitionValueInternal += dt / weather.TransitionDuration;
                if (weather.TransitionValueInternal >= 1)
                {
                    weather.Type = weather.UpcomingType;
                    weather.UpcomingType = weather.Type;
                    weather.TransitionValueInternal = 0;
                    weather.TransitionValue = 0;
                    weather.RainIntensity = weather.Type.RainIntensity;
                }
                else
                {
                    weather.TransitionValue = (ushort) (Smoothstep(0, 1, weather.TransitionValueInternal) * ushort.MaxValue);
                    weather.RainIntensity = (float) Lerp(weather.Type.RainIntensity, weather.UpcomingType.RainIntensity, weather.TransitionValueInternal);
                }
            }
            
            CalcWater(weather, Lerp(weather.Type.Sun, weather.UpcomingType.Sun, weather.TransitionValueInternal), dt / 1000.0);
            CalcGrip(weather, baseGrip, rainTrackGripReduction);
        }
    }
}