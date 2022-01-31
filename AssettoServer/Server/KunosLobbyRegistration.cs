using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using AssettoServer.Server.Configuration;
using Serilog;

namespace AssettoServer.Server;

internal class KunosLobbyRegistration
{
    private readonly ACServer _server;
    private readonly HttpClient _httpClient = new();
    
    internal KunosLobbyRegistration(ACServer server)
    {
        _server = server;
    }
    
    internal async Task LoopAsync()
    {
        if (!await RegisterToLobbyAsync())
            return;

        while (true)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1));
                await PingLobbyAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during Kunos lobby update");
            }
        }
    }

    private async Task<bool> RegisterToLobbyAsync()
    {
        ACServerConfiguration cfg = _server.Configuration;
        var builder = new UriBuilder("http://93.57.10.21/lobby.ashx/register");
        var queryParams = HttpUtility.ParseQueryString(builder.Query);
        queryParams["name"] = cfg.Name;
        queryParams["port"] = cfg.UdpPort.ToString();
        queryParams["tcp_port"] = cfg.TcpPort.ToString();
        queryParams["max_clients"] = cfg.MaxClients.ToString();
        queryParams["track"] = cfg.FullTrackName;
        queryParams["cars"] = string.Join(',', cfg.EntryCars.Select(c => c.Model).Distinct());
        queryParams["timeofday"] = ((int)cfg.SunAngle).ToString();
        queryParams["sessions"] = string.Join(',', cfg.Sessions.Select(s => (int)s.Type));
        queryParams["durations"] = string.Join(',', cfg.Sessions.Select(s => s.IsTimedRace ? s.Time * 60 : s.Laps));
        queryParams["password"] = string.IsNullOrEmpty(cfg.Password) ? "0" : "1";
        queryParams["version"] = "202";
        queryParams["pickup"] = "1";
        queryParams["autoclutch"] = cfg.AutoClutchAllowed ? "1" : "0";
        queryParams["abs"] = cfg.ABSAllowed.ToString();
        queryParams["tc"] = cfg.TractionControlAllowed.ToString();
        queryParams["stability"] = cfg.StabilityAllowed ? "1" : "0";
        queryParams["legal_tyres"] = cfg.LegalTyres;
        queryParams["fixed_setup"] = "0";
        queryParams["timed"] = "0";
        queryParams["extra"] = cfg.HasExtraLap ? "1" : "0";
        queryParams["pit"] = "0";
        queryParams["inverted"] = cfg.InvertedGridPositions.ToString();
        builder.Query = queryParams.ToString();

        Log.Information("Registering server to lobby...");
        HttpResponseMessage response = await _httpClient.GetAsync(builder.ToString());
        string body = await response.Content.ReadAsStringAsync();

        if (body.StartsWith("OK"))
        {
            Log.Information("Lobby registration successful");
            return true;
        }
        
        Log.Error("Could not register to lobby, server returned: {ErrorMessage}", body);
        return false;
    }

    private async Task PingLobbyAsync()
    {
        var builder = new UriBuilder("http://93.57.10.21/lobby.ashx/ping");
        var queryParams = HttpUtility.ParseQueryString(builder.Query);
        
        queryParams["session"] = ((int)_server.CurrentSession.Configuration.Type).ToString();
        queryParams["timeleft"] = ((int)_server.CurrentSession.TimeLeft.TotalSeconds).ToString();
        queryParams["port"] = _server.Configuration.UdpPort.ToString();
        queryParams["clients"] = _server.ConnectedCars.Count.ToString();
        queryParams["track"] = _server.Configuration.FullTrackName;
        queryParams["pickup"] = "1";
        builder.Query = queryParams.ToString();
        
        HttpResponseMessage response = await _httpClient.GetAsync(builder.ToString());
        string body = await response.Content.ReadAsStringAsync();

        if (!body.StartsWith("OK"))
        {
            Log.Error("Could not update lobby, server returned: {ErrorMessage}", body);
        }
    }
}