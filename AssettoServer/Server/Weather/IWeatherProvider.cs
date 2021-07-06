using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssettoServer.Server
{
    interface IWeatherProvider
    {
        public Task<Weather> GetWeatherAsync(float lat, float lon);
    }
}
