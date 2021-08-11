using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssettoServer.Server.Weather
{
    public interface ILiveWeatherProvider
    {
        public Task<LiveWeatherProviderResponse> GetWeatherAsync(double lat, double lon);
    }
}
