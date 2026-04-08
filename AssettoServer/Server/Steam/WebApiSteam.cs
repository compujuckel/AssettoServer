using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AssettoServer.Network.Tcp;
using AssettoServer.Server.Configuration;
using Serilog;

namespace AssettoServer.Server.Steam;

public class WebApiSteam : ISteam
{
    private readonly ACServerConfiguration _configuration;

    public WebApiSteam(ACServerConfiguration configuration) 
    {
        _configuration = configuration;
        
        if (string.IsNullOrEmpty(_configuration.Extra.SteamWebApiKey))
        {
            throw new ConfigurationException(
                "Steam Web API key is required for Steam Authentication on this platform. Visit https://steamcommunity.com/dev/apikey to get a key")
            {
                HelpLink = "https://steamcommunity.com/dev/apikey"
            };
        }
    }
    
    public async Task<SteamResult> ValidateSessionTicketAsync(byte[]? sessionTicket, ulong guid, ACTcpClient client)
    {
        if (sessionTicket == null) return new SteamResult { ErrorReason = "Missing session ticket" };

        var url = $"https://api.steampowered.com/ISteamUserAuth/AuthenticateUserTicket/v0001/?key={_configuration.Extra.SteamWebApiKey}&appid={ISteam.AppId}&ticket={Convert.ToHexString(sessionTicket)}";
        try
        {
            // for some reason I get "Invalid ticket" ticket errors when reusing the HttpClient, so just create a new one every time
            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromSeconds(10);
            
            using var response = await http.GetAsync(url);
            if (!response.IsSuccessStatusCode) return new SteamResult { ErrorReason = "Wrong status code" };

            var result = await response.Content.ReadFromJsonAsync<AuthenticateUserTicketResponse>();

            if (result == null) return new SteamResult { ErrorReason = "Empty result" };
            if (result.Response.Error != null) return new SteamResult { ErrorReason = result.Response.Error.ErrorDesc };

            if (result.Response.Params is not { Result: "OK" }) return new SteamResult { ErrorReason = "Wrong result" };
            
            return new SteamResult
            {
                Success = true,
                SteamId = ulong.Parse(result.Response.Params.SteamId),
                OwnerSteamId = ulong.Parse(result.Response.Params.OwnerSteamId)
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during Steam Web API ticket validation");
            return new SteamResult { ErrorReason = "Exception occurred" };
        }
    }
}
