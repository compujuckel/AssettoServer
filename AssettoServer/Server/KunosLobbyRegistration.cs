using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
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
        await RegisterToLobbyAsync();

        while (true)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(10));
                await PingLobbyAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during Kunos lobby registration");
            }
        }
        // ReSharper disable once FunctionNeverReturns
    }

    private async Task RegisterToLobbyAsync()
    {
        ACServerConfiguration cfg = _server.Configuration;
        Dictionary<string, object> queryParamsDict = new Dictionary<string, object>
        {
            ["name"] = cfg.Name,
            ["port"] = cfg.UdpPort,
            ["tcp_port"] = cfg.TcpPort,
            ["max_clients"] = cfg.MaxClients,
            ["track"] = cfg.FullTrackName,
            ["cars"] = string.Join(',', cfg.EntryCars.Select(c => c.Model).Distinct()),
            ["timeofday"] = (int)cfg.SunAngle,
            ["sessions"] = string.Join(',', cfg.Sessions.Select(s => s.Type)),
            ["durations"] = string.Join(',', cfg.Sessions.Select(s => s.Type == 3 ? s.Laps : s.Time * 60)),
            ["password"] = string.IsNullOrEmpty(cfg.Password) ? "0" : cfg.Password,
            ["version"] = "202",
            ["pickup"] = "1",
            ["autoclutch"] = cfg.AutoClutchAllowed ? "1" : "0",
            ["abs"] = cfg.ABSAllowed,
            ["tc"] = cfg.TractionControlAllowed,
            ["stability"] = cfg.StabilityAllowed ? "1" : "0",
            ["legal_tyres"] = cfg.LegalTyres,
            ["fixed_setup"] = "0",
            ["timed"] = "0",
            ["extra"] = cfg.HasExtraLap ? "1" : "0",
            ["pit"] = "0",
            ["inverted"] = cfg.InvertedGridPositions
        };

        Log.Information("Registering server to lobby");
        string queryString = string.Join('&', queryParamsDict.Select(p => $"{p.Key}={p.Value}"));
        HttpResponseMessage response = await _httpClient.GetAsync($"http://93.57.10.21/lobby.ashx/register?{queryString}");
        if (!response.IsSuccessStatusCode)
            Log.Information("Failed to register to lobby");
    }

    private async Task PingLobbyAsync()
    {
        Dictionary<string, object> queryParamsDict = new Dictionary<string, object>
        {
            ["session"] = _server.CurrentSession.Type,
            ["timeleft"] = (int)_server.CurrentSession.TimeLeft.TotalSeconds,
            ["port"] = _server.Configuration.UdpPort,
            ["clients"] = _server.ConnectedCars.Count,
            ["track"] = _server.Configuration.FullTrackName,
            ["pickup"] = "1"
        };

        string queryString = string.Join('&', queryParamsDict.Select(p => $"{p.Key}={p.Value}"));
        HttpResponseMessage response = await _httpClient.GetAsync($"http://93.57.10.21/lobby.ashx/ping?{queryString}");
        if (!response.IsSuccessStatusCode)
            Log.Information("Failed to send lobby ping update");
    }
}