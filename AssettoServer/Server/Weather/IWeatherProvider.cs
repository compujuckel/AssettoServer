using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssettoServer.Server.Weather
{
    public interface IWeatherProvider
    {
        public Task<WeatherProviderResponse> GetWeatherAsync(double lat, double lon);
    }
}
