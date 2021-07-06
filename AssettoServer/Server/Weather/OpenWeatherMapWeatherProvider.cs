using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace AssettoServer.Server
{
    class OpenWeatherMapWeatherProvider : IWeatherProvider
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

        private string ApiKey { get; }
        private HttpClient HttpClient { get; }

        public OpenWeatherMapWeatherProvider(string apiKey)
        {
            ApiKey = apiKey;
            HttpClient = new HttpClient();
        }
        public async Task<Weather> GetWeatherAsync(float lat, float lon)
        {
            HttpResponseMessage response = await HttpClient.GetAsync($"https://api.openweathermap.org/data/2.5/weather?appid={ApiKey}&units=metric&lat={lat}&lon={lon}");
            if(response.IsSuccessStatusCode)
            {
                JObject json = JObject.Parse(await response.Content.ReadAsStringAsync());
                Weather weather = new Weather
                {
                    Graphics = TranslateIdToGraphics((OpenWeatherType)(int)json.SelectToken("weather[0].id")),
                    TemperatureAmbient = (float)json.SelectToken("main.temp"),
                    TemperatureRoad = (float)json.SelectToken("main.temp"),
                    Pressure = (int)json.SelectToken("main.pressure"),
                    Humidity = (int)json.SelectToken("main.humidity"),
                    WindSpeed = (float)json.SelectToken("wind.speed"),
                    WindDirection = (int)json.SelectToken("wind.deg")
                };

                return weather;
            }

            return null;
        }

        // Adapted from https://github.com/gro-ove/actools/blob/master/AcManager.Tools/Helpers/Api/OpenWeatherApiProvider.cs
        private string TranslateIdToGraphics(OpenWeatherType id)
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
                    return "sol_42_thunderstorm";

                case OpenWeatherType.LightThunderstorm:
                    return "sol_41_light_thunderstorm";

                case OpenWeatherType.HeavyThunderstorm:
                case OpenWeatherType.TropicalStorm:
                    return "sol_43_heavy_thunderstorm";

                case OpenWeatherType.LightIntensityDrizzle:
                case OpenWeatherType.LightIntensityDrizzleRain:
                    return "sol_31_light_drizzle";

                case OpenWeatherType.Drizzle:
                case OpenWeatherType.DrizzleRain:
                case OpenWeatherType.ShowerDrizzle:
                    return "sol_32_drizzle";

                case OpenWeatherType.HeavyIntensityDrizzle:
                case OpenWeatherType.HeavyIntensityDrizzleRain:
                    return "sol_33_heavy_drizzle";

                case OpenWeatherType.LightRain:
                case OpenWeatherType.LightIntensityShowerRain:
                    return "sol_34_light_rain";

                case OpenWeatherType.ModerateRain:
                case OpenWeatherType.FreezingRain:
                case OpenWeatherType.ShowerRainAndDrizzle:
                case OpenWeatherType.ShowerRain:
                case OpenWeatherType.RaggedShowerRain:
                    return "sol_35_rain";

                case OpenWeatherType.HeavyIntensityRain:
                case OpenWeatherType.VeryHeavyRain:
                case OpenWeatherType.ExtremeRain:
                case OpenWeatherType.HeavyShowerRainAndDrizzle:
                case OpenWeatherType.HeavyIntensityShowerRain:
                    return "sol_36_heavy_rain";

                case OpenWeatherType.LightSnow:
                case OpenWeatherType.LightShowerSnow:
                    return "sol_51_light_snow";

                case OpenWeatherType.Snow:
                case OpenWeatherType.ShowerSnow:
                    return "sol_52_snow";

                case OpenWeatherType.HeavySnow:
                case OpenWeatherType.HeavyShowerSnow:
                    return "sol_53_heavy_snow";

                case OpenWeatherType.LightRainAndSnow:
                    return "sol_54_light_sleet";

                case OpenWeatherType.RainAndSnow:
                case OpenWeatherType.Sleet:
                    return "sol_55_sleet";

                case OpenWeatherType.ShowerSleet:
                    return "sol_56_heavy_sleet";

                case OpenWeatherType.Mist:
                    return "sol_11_mist";

                case OpenWeatherType.Smoke:
                    return "sol_24_smoke";

                case OpenWeatherType.Haze:
                    return "sol_21_haze";

                case OpenWeatherType.Sand:
                case OpenWeatherType.SandAndDustWhirls:
                    return "sol_23_sand";

                case OpenWeatherType.Dust:
                case OpenWeatherType.VolcanicAsh:
                    return "sol_22_dust";

                case OpenWeatherType.Fog:
                    return "sol_12_fog";

                case OpenWeatherType.Squalls:
                    return "sol_44_squalls";

                case OpenWeatherType.Tornado:
                case OpenWeatherType.TornadoExtreme:
                    return "sol_45_tornado";

                case OpenWeatherType.ClearSky:
                case OpenWeatherType.Calm:
                case OpenWeatherType.LightBreeze:
                    return "sol_01_clear";

                case OpenWeatherType.FewClouds:
                case OpenWeatherType.GentleBreeze:
                case OpenWeatherType.ModerateBreeze:
                    return "sol_02_few_clouds";

                case OpenWeatherType.ScatteredClouds:
                    return "sol_03_scattered_clouds";

                case OpenWeatherType.BrokenClouds:
                    return "sol_05_broken_clouds";

                case OpenWeatherType.OvercastClouds:
                    return "sol_06_overcast";

                case OpenWeatherType.Hurricane:
                case OpenWeatherType.Gale:
                case OpenWeatherType.SevereGale:
                case OpenWeatherType.Storm:
                case OpenWeatherType.ViolentStorm:
                case OpenWeatherType.HurricaneAdditional:
                    return "sol_46_hurricane";

                case OpenWeatherType.Cold:
                    return "sol_01_clear";

                case OpenWeatherType.Hot:
                    return "sol_01_clear";

                case OpenWeatherType.Windy:
                case OpenWeatherType.FreshBreeze:
                case OpenWeatherType.StrongBreeze:
                case OpenWeatherType.HighWind:
                    return "sol_04_windy";

                case OpenWeatherType.Hail:
                    return "sol_57_hail";
            }

            // TODO log
            return "sol_00_no_clouds";

        }
    }
}
