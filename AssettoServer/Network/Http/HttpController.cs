using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AssettoServer.Server;
using AssettoServer.Server.Admin;
using AssettoServer.Server.CMContentProviders;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.GeoParams;
using AssettoServer.Server.OpenSlotFilters;
using AssettoServer.Server.Weather;
using AssettoServer.Shared.Network.Http.Responses;
using AssettoServer.Shared.Weather;
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
    private readonly IAdminService _adminService;
    private readonly OpenSlotFilterChain _openSlotFilter;
    private readonly HttpInfoCache _cache;
    private readonly ICMContentProvider _contentProvider;

    public HttpController(CSPServerScriptProvider serverScriptProvider,
        WeatherManager weatherManager,
        SessionManager sessionManager,
        ACServerConfiguration configuration,
        EntryCarManager entryCarManager,
        GeoParamsManager geoParamsManager,
        CSPFeatureManager cspFeatureManager,
        IAdminService adminService,
        OpenSlotFilterChain openSlotFilter,
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
        _adminService = adminService;
        _openSlotFilter = openSlotFilter;
        _cache = cache;
        _contentProvider = contentProvider;
    }

    [EnableCors("ServerQueryPolicy")]
    [HttpGet("/INFO")]
    public InfoResponse GetInfo()
    {
        InfoResponse responseObj = new InfoResponse
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

        return responseObj;
    }

    [EnableCors("ServerQueryPolicy")]
    [HttpGet("/JSON{guid}")]
    public async Task<EntryListResponse> GetEntryList(string guid)
    {
        guid = guid.Substring(1);
        bool guidValid = ulong.TryParse(guid, out ulong ulongGuid);
        bool isAdmin = guidValid && await _adminService.IsAdminAsync(ulongGuid);

        var cars = new List<EntryListResponseCar>(_entryCarManager.EntryCars.Length);
        foreach (var ec in _entryCarManager.EntryCars)
        {
            cars.Add(new EntryListResponseCar
            {
                Model = ec.Model,
                Skin = ec.Skin,
                IsEntryList = isAdmin || await _openSlotFilter.IsSlotOpen(ec, ulongGuid),
                DriverName = ec.Client?.Name,
                DriverTeam = ec.Client?.Team,
                IsConnected = ec.Client != null
            });
        }
        EntryListResponse responseObj = new EntryListResponse
        {
            Cars = cars,
            Features = _cspFeatureManager.Features.Keys
        };

        return responseObj;
    }

    [EnableCors("ServerQueryPolicy")]
    [HttpGet("/api/details")]
    public async Task<DetailResponse> GetDetails(string? guid)
    {
        bool guidValid = ulong.TryParse(guid, out ulong ulongGuid);
        bool isAdmin = guidValid && await _adminService.IsAdminAsync(ulongGuid);
        
        var cars = new List<DetailResponseCar>(_entryCarManager.EntryCars.Length);
        foreach (var ec in _entryCarManager.EntryCars)
        {
            cars.Add(new DetailResponseCar
            {
                Model = ec.Model,
                Skin = ec.Skin,
                IsEntryList = isAdmin || await _openSlotFilter.IsSlotOpen(ec, ulongGuid),
                DriverName = ec.Client?.Name,
                DriverTeam = ec.Client?.Team,
                DriverNation = ec.Client?.NationCode,
                IsConnected = ec.Client != null,
                ID = ec.Client?.HashedGuid
            });
        }
        
        DetailResponse responseObj = new DetailResponse
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
        
        return responseObj;
    }

    [HttpGet("/api/scripts/{scriptId:int}")]
    public IActionResult GetScript(int scriptId)
    {
        return scriptId < _serverScriptProvider.Scripts.Count ? _serverScriptProvider.Scripts[scriptId]() : NotFound();
    }
}
