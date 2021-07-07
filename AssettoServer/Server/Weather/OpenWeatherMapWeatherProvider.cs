using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace AssettoServer.Server.Weather
{
    public class OpenWeatherMapWeatherProvider : IWeatherProvider
    {
        public enum OpenWeatherType
        {
            ThunderstormWithLightRain = 200,
            ThunderstormWithRain = 201,
            ThunderstormWithHeavyRain = 202,
            LightThunderstorm = 210,
            Thunderstorm = 211,
            HeavyThunderstorm = 212,
            RaggedThunderstorm = 221,
            ThunderstormWithLightDrizzle = 230,
            ThunderstormWithDrizzle = 231,
            ThunderstormWithHeavyDrizzle = 232,
            LightIntensityDrizzle = 300,
            Drizzle = 301,
            HeavyIntensityDrizzle = 302,
            LightIntensityDrizzleRain = 310,
            DrizzleRain = 311,
            HeavyIntensityDrizzleRain = 312,
            ShowerRainAndDrizzle = 313,
            HeavyShowerRainAndDrizzle = 314,
            ShowerDrizzle = 321,
            LightRain = 500,
            ModerateRain = 501,
            HeavyIntensityRain = 502,
            VeryHeavyRain = 503,
            ExtremeRain = 504,
            FreezingRain = 511,
            LightIntensityShowerRain = 520,
            ShowerRain = 521,
            HeavyIntensityShowerRain = 522,
            RaggedShowerRain = 531,
            LightSnow = 600,
            Snow = 601,
            HeavySnow = 602,
            Sleet = 611,
            ShowerSleet = 612,
            LightRainAndSnow = 615,
            RainAndSnow = 616,
            LightShowerSnow = 620,
            ShowerSnow = 621,
            HeavyShowerSnow = 622,
            Mist = 701,
            Smoke = 711,
            Haze = 721,
            SandAndDustWhirls = 731,
            Fog = 741,
            Sand = 751,
            Dust = 761,
            VolcanicAsh = 762,
            Squalls = 771,
            Tornado = 781,
            ClearSky = 800,
            FewClouds = 801,
            ScatteredClouds = 802,
            BrokenClouds = 803,
            OvercastClouds = 804,
            TornadoExtreme = 900,
            TropicalStorm = 901,
            Hurricane = 902,
            Cold = 903,
            Hot = 904,
            Windy = 905,
            Hail = 906,
            Calm = 951,
            LightBreeze = 952,
            GentleBreeze = 953,
            ModerateBreeze = 954,
            FreshBreeze = 955,
            StrongBreeze = 956,
            HighWind, NearGale = 957,
            Gale = 958,
            SevereGale = 959,
            Storm = 960,
            ViolentStorm = 961,
            HurricaneAdditional = 962,
        }

        private readonly string _apiKey;
        private readonly HttpClient _httpClient;

        public OpenWeatherMapWeatherProvider(string apiKey)
        {
            _apiKey = apiKey;
            _httpClient = new HttpClient();
        }
        public async Task<WeatherProviderResponse> GetWeatherAsync(float lat, float lon)
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"https://api.openweathermap.org/data/2.5/weather?appid={_apiKey}&units=metric&lat={lat}&lon={lon}");
            if(response.IsSuccessStatusCode)
            {
                JObject json = JObject.Parse(await response.Content.ReadAsStringAsync());
                WeatherProviderResponse weather = new WeatherProviderResponse
                {
                    WeatherType = TranslateIdToWeatherType((OpenWeatherType)(int)json.SelectToken("weather[0].id")),
                    TemperatureAmbient = (float)json.SelectToken("main.temp"),
                    Pressure = (int)json.SelectToken("main.pressure"),
                    Humidity = (int)json.SelectToken("main.humidity"),
                    WindSpeed = (float)json.SelectToken("wind.speed") * 3.6f,
                    WindDirection = (int)json.SelectToken("wind.deg")
                };

                return weather;
            }

            return null;
        }

        // Adapted from https://github.com/gro-ove/actools/blob/master/AcManager.Tools/Helpers/Api/OpenWeatherApiProvider.cs
        private CMWeatherType TranslateIdToWeatherType(OpenWeatherType id)
        {
            switch (id)
            {
                case OpenWeatherType.RaggedThunderstorm:
                case OpenWeatherType.Thunderstorm:
                case OpenWeatherType.ThunderstormWithLightRain:
                case OpenWeatherType.ThunderstormWithRain:
                case OpenWeatherType.ThunderstormWithHeavyRain:
                case OpenWeatherType.ThunderstormWithLightDrizzle:
                case OpenWeatherType.ThunderstormWithDrizzle:
                case OpenWeatherType.ThunderstormWithHeavyDrizzle:
                    return CMWeatherType.Thunderstorm;

                case OpenWeatherType.LightThunderstorm:
                    return CMWeatherType.LightThunderstorm;

                case OpenWeatherType.HeavyThunderstorm:
                case OpenWeatherType.TropicalStorm:
                    return CMWeatherType.HeavyThunderstorm;

                case OpenWeatherType.LightIntensityDrizzle:
                case OpenWeatherType.LightIntensityDrizzleRain:
                    return CMWeatherType.LightDrizzle;

                case OpenWeatherType.Drizzle:
                case OpenWeatherType.DrizzleRain:
                case OpenWeatherType.ShowerDrizzle:
                    return CMWeatherType.Drizzle;

                case OpenWeatherType.HeavyIntensityDrizzle:
                case OpenWeatherType.HeavyIntensityDrizzleRain:
                    return CMWeatherType.HeavyDrizzle;

                case OpenWeatherType.LightRain:
                case OpenWeatherType.LightIntensityShowerRain:
                    return CMWeatherType.LightRain;

                case OpenWeatherType.ModerateRain:
                case OpenWeatherType.FreezingRain:
                case OpenWeatherType.ShowerRainAndDrizzle:
                case OpenWeatherType.ShowerRain:
                case OpenWeatherType.RaggedShowerRain:
                    return CMWeatherType.Rain;

                case OpenWeatherType.HeavyIntensityRain:
                case OpenWeatherType.VeryHeavyRain:
                case OpenWeatherType.ExtremeRain:
                case OpenWeatherType.HeavyShowerRainAndDrizzle:
                case OpenWeatherType.HeavyIntensityShowerRain:
                    return CMWeatherType.HeavyRain;

                case OpenWeatherType.LightSnow:
                case OpenWeatherType.LightShowerSnow:
                    return CMWeatherType.LightSnow;

                case OpenWeatherType.Snow:
                case OpenWeatherType.ShowerSnow:
                    return CMWeatherType.Snow;

                case OpenWeatherType.HeavySnow:
                case OpenWeatherType.HeavyShowerSnow:
                    return CMWeatherType.HeavySnow;

                case OpenWeatherType.LightRainAndSnow:
                    return CMWeatherType.LightSleet;

                case OpenWeatherType.RainAndSnow:
                case OpenWeatherType.Sleet:
                    return CMWeatherType.Sleet;

                case OpenWeatherType.ShowerSleet:
                    return CMWeatherType.HeavySleet;

                case OpenWeatherType.Mist:
                    return CMWeatherType.Mist;

                case OpenWeatherType.Smoke:
                    return CMWeatherType.Smoke;

                case OpenWeatherType.Haze:
                    return CMWeatherType.Haze;

                case OpenWeatherType.Sand:
                case OpenWeatherType.SandAndDustWhirls:
                    return CMWeatherType.Sand;

                case OpenWeatherType.Dust:
                case OpenWeatherType.VolcanicAsh:
                    return CMWeatherType.Dust;

                case OpenWeatherType.Fog:
                    return CMWeatherType.Fog;

                case OpenWeatherType.Squalls:
                    return CMWeatherType.Squalls;

                case OpenWeatherType.Tornado:
                case OpenWeatherType.TornadoExtreme:
                    return CMWeatherType.Tornado;

                case OpenWeatherType.ClearSky:
                case OpenWeatherType.Calm:
                case OpenWeatherType.LightBreeze:
                    return CMWeatherType.Clear;

                case OpenWeatherType.FewClouds:
                case OpenWeatherType.GentleBreeze:
                case OpenWeatherType.ModerateBreeze:
                    return CMWeatherType.FewClouds;

                case OpenWeatherType.ScatteredClouds:
                    return CMWeatherType.ScatteredClouds;

                case OpenWeatherType.BrokenClouds:
                    return CMWeatherType.BrokenClouds;

                case OpenWeatherType.OvercastClouds:
                    return CMWeatherType.OvercastClouds;

                case OpenWeatherType.Hurricane:
                case OpenWeatherType.Gale:
                case OpenWeatherType.SevereGale:
                case OpenWeatherType.Storm:
                case OpenWeatherType.ViolentStorm:
                case OpenWeatherType.HurricaneAdditional:
                    return CMWeatherType.Hurricane;

                case OpenWeatherType.Cold:
                    return CMWeatherType.Cold;

                case OpenWeatherType.Hot:
                    return CMWeatherType.Hot;

                case OpenWeatherType.Windy:
                case OpenWeatherType.FreshBreeze:
                case OpenWeatherType.StrongBreeze:
                case OpenWeatherType.HighWind:
                    return CMWeatherType.Windy;

                case OpenWeatherType.Hail:
                    return CMWeatherType.Hail;

                default:
                    return CMWeatherType.Clear;
            }
        }
    }
}
