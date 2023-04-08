using AssettoServer.Shared.Http.Responses;
using Newtonsoft.Json;

namespace AssettoServer.Shared.Http;

public class ServerQueryClient
{
    private static readonly HttpClient Client = new();

    public ServerQueryClient()
    {
        Client.Timeout = TimeSpan.FromSeconds(5);
    }

    public async Task<InfoResponse?> GetInfoAsync(string host)
    {
        var response = await Client.GetAsync($"http://{host}/INFO").ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<InfoResponse>(content);
    }
    
    public async Task<EntryListResponse?> GetEntryListAsync(string host, ulong? guid = null)
    {
        var response = await Client.GetAsync($"http://{host}/JSON|{guid}").ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<EntryListResponse>(content);
    }
    
    public async Task<DetailResponse?> GetDetailsAsync(string host, ulong? guid = null)
    {
        var response = await Client.GetAsync($"http://{host}/api/details?guid={guid}").ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<DetailResponse>(content);
    }
}
