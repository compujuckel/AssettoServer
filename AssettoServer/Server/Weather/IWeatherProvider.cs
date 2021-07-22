using System.Threading.Tasks;

namespace AssettoServer.Server.Weather
{
    public interface IWeatherProvider
    {
        public Task UpdateAsync();
    }
}