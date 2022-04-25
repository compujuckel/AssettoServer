using System.Threading.Tasks;

namespace AssettoServer.Server.GeoParams;

public interface IGeoParamsProvider
{
    public Task<GeoParams?> GetAsync();
}
