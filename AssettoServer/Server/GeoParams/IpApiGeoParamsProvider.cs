using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace AssettoServer.Server.GeoParams;

public class IpApiGeoParamsProvider : IGeoParamsProvider
{
    private readonly HttpClient _httpClient;

    public IpApiGeoParamsProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<GeoParams?> GetAsync()
    {
        var response = await _httpClient.GetAsync("http://ip-api.com/json");

        if (!response.IsSuccessStatusCode) return null;
        
        var json = await response.Content.ReadFromJsonAsync<IpApiResponse>() ?? throw new JsonException("Cannot deserialize ip-api.com response");
        return new GeoParams
        {
            Ip = json.Query,
            City = json.City,
            Country = json.Country,
            CountryCode = json.CountryCode
        };
    }

    private class IpApiResponse
    {
        public required string Query { get; init; }
        public required string City { get; init; }
        public required string Country { get; init; }
        public required string CountryCode { get; init; }
    }
}
