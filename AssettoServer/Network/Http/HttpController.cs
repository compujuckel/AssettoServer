using System;
using System.Linq;
using System.Threading.Tasks;
using AssettoServer.Server;
using AssettoServer.Server.CMContentProviders;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.GeoParams;
using AssettoServer.Server.Weather;
using AssettoServer.Shared.Network.Http.Responses;
using AssettoServer.Shared.Weather;
using AssettoServer.Utils;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace AssettoServer.Network.Http;

[ApiController]
public class HttpController : ControllerBase
{
    private readonly ACServerConfiguration _configuration;
    private readonly CSPServerScriptProvider _serverScriptProvider;
    private readonly WeatherManager _weatherManager;
    private readonly SessionManager _sessionManager;
    private readonly EntryCarManager _entryCarManager;
    private readonly GeoParamsManager _geoParamsManager;
    private readonly CSPFeatureManager _cspFeatureManager;
    private readonly HttpInfoCache _cache;
    private readonly ICMContentProvider _contentProvider;

    public HttpController(CSPServerScriptProvider serverScriptProvider,
        WeatherManager weatherManager,
        SessionManager sessionManager,
        ACServerConfiguration configuration,
        EntryCarManager entryCarManager,
        GeoParamsManager geoParamsManager,
        CSPFeatureManager cspFeatureManager,
        HttpInfoCache cache,
        ICMContentProvider contentProvider)
    {
        _serverScriptProvider = serverScriptProvider;
        _weatherManager = weatherManager;
        _sessionManager = sessionManager;
        _configuration = configuration;
        _entryCarManager = entryCarManager;
        _geoParamsManager = geoParamsManager;
        _cspFeatureManager = cspFeatureManager;
        _cache = cache;
        _contentProvider = contentProvider;
    }

    [EnableCors("ServerQueryPolicy")]
    [HttpGet("/INFO")]
    public InfoResponse GetInfo()
    {
        return new InfoResponse
        {
            Cars = _cache.Cars,
            Clients = _entryCarManager.ConnectedCars.Count,
            Country = _cache.Country,
            CPort = _configuration.Server.HttpPort,
            Durations = _cache.Durations,
            Extra = _configuration.Server.HasExtraLap,
            Inverted = _configuration.Server.InvertedGridPositions,
            Ip = _geoParamsManager.GeoParams.Ip,
            MaxClients = _configuration.Server.MaxClients,
            Name = _cache.ServerName,
            Pass = !string.IsNullOrEmpty(_configuration.Server.Password),
            Pickup = true,
            Pit = _configuration.Server.PitWindowEnd > 0,
            Session = (int)_sessionManager.CurrentSession.Configuration.Type,
            Port = _configuration.Server.UdpPort,
            SessionTypes = _cache.SessionTypes,
            Timed = _configuration.Sessions.All(s => s.IsTimedRace),
            TimeLeft = _sessionManager.CurrentSession.TimeLeftMilliseconds / 1000,
            TimeOfDay = (int)WeatherUtils.SunAngleFromTicks(_weatherManager.CurrentDateTime.TimeOfDay.TickOfDay),
            Timestamp = 0,
            TPort = _configuration.Server.TcpPort,
            Track = _cache.Track,
            PoweredBy = _cache.PoweredBy
        };
    }

    [EnableCors("ServerQueryPolicy")]
    [HttpGet("/JSON{guid}")]
    public async Task<EntryListResponse> GetJson(string guid)
    {
        _ = ulong.TryParse(guid[1..], out ulong ulongGuid);

        return new EntryListResponse
        {
            Cars = await _entryCarManager.GetEntryListAsync(ulongGuid),
            Features = _cspFeatureManager.Features.Keys
        };
    }

    [EnableCors("ServerQueryPolicy")]
    [HttpGet("/api/details")]
    public async Task<DetailResponse> GetDetails(string? guid)
    {
        _ = ulong.TryParse(guid, out ulong ulongGuid);
        var cars = await _entryCarManager.GetEntryListAsync(ulongGuid);
        
        return new DetailResponse
        {
            Cars = _cache.Cars,
            Clients = _entryCarManager.ConnectedCars.Count,
            Country = _cache.Country,
            CPort = _configuration.Server.HttpPort,
            Durations = _cache.Durations,
            Extra = _configuration.Server.HasExtraLap,
            Inverted = _configuration.Server.InvertedGridPositions,
            Ip = _geoParamsManager.GeoParams.Ip,
            MaxClients = _configuration.Server.MaxClients,
            Name = _configuration.Server.Name,
            Pass = !string.IsNullOrEmpty(_configuration.Server.Password),
            Pickup = true,
            Pit = _configuration.Server.PitWindowEnd > 0,
            Session = (int)_sessionManager.CurrentSession.Configuration.Type,
            Port = _configuration.Server.UdpPort,
            SessionTypes = _cache.SessionTypes,
            Timed = _configuration.Sessions.All(s => s.IsTimedRace),
            TimeLeft = _sessionManager.CurrentSession.TimeLeftMilliseconds / 1000,
            TimeOfDay = (int)WeatherUtils.SunAngleFromTicks(_weatherManager.CurrentDateTime.TimeOfDay.TickOfDay),
            Timestamp = 0,
            TPort = _configuration.Server.TcpPort,
            Track = _cache.Track,
            LoadingImageUrl = _configuration.Extra.LoadingImageUrls is { Count: > 0 }
                ? _configuration.Extra.LoadingImageUrls[Random.Shared.Next(0, _configuration.Extra.LoadingImageUrls.Count)]
                : null,
            Players = new DetailResponsePlayerList { Cars = cars },
            Until = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + _sessionManager.CurrentSession.TimeLeftMilliseconds / 1000,
            Content = await _contentProvider.GetContentAsync(ulongGuid),
            TrackBase = _configuration.Server.Track,
            City = _geoParamsManager.GeoParams.City,
            Frequency = _configuration.Server.RefreshRateHz,
            Assists = _cache.Assists,
            WrappedPort = _configuration.Server.HttpPort,
            AmbientTemperature = _weatherManager.CurrentWeather.TemperatureAmbient,
            RoadTemperature = _weatherManager.CurrentWeather.TemperatureRoad,
            CurrentWeatherId = _weatherManager.CurrentWeather.Type.WeatherFxType == WeatherFxType.None
                ? _weatherManager.CurrentWeather.Type.Graphics
                : _weatherManager.CurrentWeather.Type.WeatherFxType.ToString(),
            WindSpeed = (int)_weatherManager.CurrentWeather.WindSpeed,
            WindDirection = _weatherManager.CurrentWeather.WindDirection,
            Description = _configuration.Extra.ServerDescription,
            Grip = _weatherManager.CurrentWeather.TrackGrip * 100,
            Features = _cspFeatureManager.Features.Keys,
            PoweredBy = _cache.PoweredBy,
            Extensions = _cache.Extensions
        };
    }

    [HttpGet("/join")]
    public IActionResult Join()
    {
        string host = _configuration.Extra.JoinHost.ToLowerInvariant() switch
        {
            "wan" => _geoParamsManager.GeoParams.Ip,
            "lan" => NetworkUtils.GetPrimaryIpAddress().ToString(),
            _ => _configuration.Extra.JoinHost
        };

        if (string.IsNullOrWhiteSpace(host))
        {
            return StatusCode(503, "The server address could not be determined. Configure JoinHost in extra_cfg.yml.");
        }

        string target = $"https://acstuff.club/s/q:race/online/join?httpPort={_configuration.Server.HttpPort}&ip={Uri.EscapeDataString(host)}";
        return Redirect(target);
    }

    [HttpGet("/api/scripts/{scriptId:int}")]
    public IActionResult GetScript(int scriptId)
    {
        return scriptId < _serverScriptProvider.Scripts.Count ? _serverScriptProvider.Scripts[scriptId]() : NotFound();
    }
}
