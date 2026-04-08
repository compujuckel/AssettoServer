using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using AssettoServer.Server.Configuration;
using AssettoServer.Utils;
using Microsoft.Extensions.Hosting;
using Polly;
using Serilog;

namespace AssettoServer.Server;

public class KunosLobbyRegistration : BackgroundService
{
    private readonly ACServerConfiguration _configuration;
    private readonly SessionManager _sessionManager;
    private readonly EntryCarManager _entryCarManager;
    private readonly HttpClient _httpClient;

    public KunosLobbyRegistration(ACServerConfiguration configuration, SessionManager sessionManager, EntryCarManager entryCarManager, HttpClient httpClient)
    {
        _configuration = configuration;
        _sessionManager = sessionManager;
        _entryCarManager = entryCarManager;
        _httpClient = httpClient;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.Server.RegisterToLobby)
            return;

        try
        {
            await RegisterToLobbyWithRetryAsync(stoppingToken);
        }
        catch (TaskCanceledException) { }
        catch (KunosLobbyException ex) when (ex.Message == "ERROR,INVALID SERVER,CHECK YOUR PORT FORWARDING SETTINGS")
        {
            PrintPortForwardingHelp();
            return;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during Kunos lobby registration");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                await Policy
                    .Handle<KunosLobbyException>()
                    .Or<HttpRequestException>()
                    .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(attempt * 10))
                    .ExecuteAsync(PingLobbyWithRetryAsync, stoppingToken);
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during Kunos lobby update");
            }
        }
    }

    private void PrintPortForwardingHelp()
    {
        var localIp = NetworkUtils.GetPrimaryIpAddress();
        var routerIp = NetworkUtils.GetGatewayAddressForInterfaceWithIpAddress(localIp);
        Log.Error("""
                  Your ports are not forwarded correctly or your firewall is blocking the connection.
                  The server will continue to run, but players outside of your network won't be able to join.
                  To fix this, check your firewall settings or go into your router settings and create Port Forwards for these ports:
                  Port {UdpPort} UDP
                  Port {TcpPort} TCP
                  Port {HttpPort} TCP
                  Local IP: {LocalIp}
                  Router Page: {RouterPage}
                  Since instructions are different for each router, search in Google for "how to port forward" with the name of your router and/or ISP.
                  """, _configuration.Server.UdpPort, _configuration.Server.TcpPort, _configuration.Server.HttpPort, localIp, routerIp != null ? $"http://{routerIp}/" : "unknown");
    }

    private async Task RegisterToLobbyWithRetryAsync(CancellationToken token)
    {
        try
        {
            await RegisterToLobbyAsync("http://93.57.10.21/lobby.ashx/register", token);
        }
        catch (HttpRequestException ex)
        {
            Log.Warning(ex, "Exception querying first lobby server, trying second server in 10 seconds...");
            await Task.Delay(TimeSpan.FromSeconds(10), token);
            await RegisterToLobbyAsync("http://lobby.assettocorsa.net/lobby.ashx/register", token);
        }
    }

    private async Task RegisterToLobbyAsync(string url, CancellationToken token)
    {
        var cfg = _configuration.Server;
        var builder = new UriBuilder(url);
        var queryParams = HttpUtility.ParseQueryString(builder.Query);

        string cars = string.Join(',', _entryCarManager.EntryCars.Select(c => c.Model).Distinct());
        
        // Truncate cars list, Lobby will return 404 when the URL is too long
        const int maxLen = 1200;
        if (cars.Length > maxLen)
        {
            cars = cars[..maxLen];
            int last = cars.LastIndexOf(',');
            cars = cars[..last];
        }
        
        queryParams["name"] = cfg.Name + (_configuration.Extra.EnableServerDetails ? $" ℹ{_configuration.Server.HttpPort}" : "");
        queryParams["port"] = cfg.UdpPort.ToString();
        queryParams["tcp_port"] = cfg.TcpPort.ToString();
        queryParams["max_clients"] = cfg.MaxClients.ToString();
        queryParams["track"] = _configuration.FullTrackName;
        queryParams["cars"] = cars;
        queryParams["timeofday"] = ((int)cfg.SunAngle).ToString();
        queryParams["sessions"] = string.Join(',', _configuration.Sessions.Select(s => (int)s.Type));
        queryParams["durations"] = string.Join(',', _configuration.Sessions.Select(s => s.IsTimedRace ? s.Time * 60 : s.Laps));
        queryParams["password"] = string.IsNullOrEmpty(cfg.Password) ? "0" : "1";
        queryParams["version"] = "202";
        queryParams["pickup"] = "1";
        queryParams["autoclutch"] = cfg.AutoClutchAllowed ? "1" : "0";
        queryParams["abs"] = cfg.ABSAllowed.ToString();
        queryParams["tc"] = cfg.TractionControlAllowed.ToString();
        queryParams["stability"] = cfg.StabilityAllowed ? "1" : "0";
        queryParams["legal_tyres"] = cfg.LegalTyres;
        queryParams["fixed_setup"] = _configuration.EntryList.Cars.Any(c => c.FixedSetup != null) ? "1" : "0";
        queryParams["timed"] = _configuration.Sessions.All(s => s.IsTimedRace) ? "1" : "0";
        queryParams["extra"] = cfg.HasExtraLap ? "1" : "0";
        queryParams["pit"] = cfg.PitWindowEnd > 0 ? "1" : "0";
        queryParams["inverted"] = cfg.InvertedGridPositions.ToString();
        builder.Query = queryParams.ToString();

        Log.Information("Registering server to lobby...");
        HttpResponseMessage response = await _httpClient.GetAsync(builder.ToString(), token);
        
        response.EnsureSuccessStatusCode();
        
        string body = await response.Content.ReadAsStringAsync(token);

        if (!body.StartsWith("OK"))
        {
            throw new KunosLobbyException(body);
        }
        
        Log.Information("Lobby registration successful");
    }

    private async Task PingLobbyWithRetryAsync(CancellationToken token)
    {
        try
        {
            await PingLobbyAsync("http://93.57.10.21/lobby.ashx/ping", token);
        }
        catch (HttpRequestException ex)
        {
            Log.Warning(ex, "Exception querying first lobby server, trying second server in 10 seconds...");
            await Task.Delay(TimeSpan.FromSeconds(10), token);
            await PingLobbyAsync("http://lobby.assettocorsa.net/lobby.ashx/ping", token);
        }
    }

    private async Task PingLobbyAsync(string url, CancellationToken token)
    {
        var builder = new UriBuilder(url);
        var queryParams = HttpUtility.ParseQueryString(builder.Query);
        
        queryParams["session"] = ((int)_sessionManager.CurrentSession.Configuration.Type).ToString();
        queryParams["timeleft"] = (_sessionManager.CurrentSession.TimeLeftMilliseconds / 1000).ToString();
        queryParams["port"] = _configuration.Server.UdpPort.ToString();
        queryParams["clients"] = _entryCarManager.ConnectedCars.Count.ToString();
        queryParams["track"] = _configuration.FullTrackName;
        queryParams["pickup"] = "1";
        builder.Query = queryParams.ToString();
        
        HttpResponseMessage response = await _httpClient.GetAsync(builder.ToString(), token);

        response.EnsureSuccessStatusCode();
        
        string body = await response.Content.ReadAsStringAsync(token);

        if (!body.StartsWith("OK"))
        {
            if (body is "ERROR - RESTART YOUR SERVER TO REGISTER WITH THE LOBBY" 
                or "ERROR,SERVER NOT REGISTERED WITH LOBBY - PLEASE RESTART")
            {
                await RegisterToLobbyWithRetryAsync(token);
            }
            else
            {
                throw new KunosLobbyException(body);
            }
        }
    }
}

public class KunosLobbyException : Exception
{
    public KunosLobbyException()
    {
    }

    public KunosLobbyException(string message)
        : base(message)
    {
    }

    public KunosLobbyException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
