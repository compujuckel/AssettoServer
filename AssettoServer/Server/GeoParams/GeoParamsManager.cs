using System;
using System.Threading.Tasks;
using AssettoServer.Server.Configuration;
using Serilog;

namespace AssettoServer.Server.GeoParams;

public class GeoParamsManager
{
    private readonly IGeoParamsProvider _geoParamsProvider;
    private readonly ACServerConfiguration _configuration;

    public GeoParams GeoParams { get; private set; } = new();

    public GeoParamsManager(IGeoParamsProvider geoParamsProvider, ACServerConfiguration configuration)
    {
        _geoParamsProvider = geoParamsProvider;
        _configuration = configuration;
    }

    public async Task InitializeAsync()
    {
        try
        {
            var ret = await _geoParamsProvider.GetAsync();

            if (ret != null)
            {
                GeoParams = ret;
            }
        }
        catch (TaskCanceledException) { }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to get IP geolocation parameters");
        }

        if (_configuration.Extra.GeoParamsCountryOverride != null)
        {
            GeoParams.City = "";
            GeoParams.Country = _configuration.Extra.GeoParamsCountryOverride[0];
            GeoParams.CountryCode = _configuration.Extra.GeoParamsCountryOverride[1];
        }
    }
}
