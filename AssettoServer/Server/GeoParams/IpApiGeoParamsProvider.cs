using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

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

        if (response.IsSuccessStatusCode)
        {
            string jsonString = await response.Content.ReadAsStringAsync();
            Dictionary<string, string> json = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonString) ?? throw new JsonException("Cannot deserialize ip-api.com response");
            return new GeoParams
            {
                Ip = json["query"],
                City = json["city"],
                Country = json["country"],
                CountryCode = json["countryCode"]
            };
        }

        return null;
    }
}
