using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AssettoServer.Network.Http.Responses;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.GeoParams;
using AssettoServer.Server.Weather;
using Microsoft.AspNetCore.Mvc;

namespace AssettoServer.Network.Http
{
    [ApiController]
    public class HttpController : ControllerBase
    {

        private readonly ACServer _server;
        private readonly ACServerConfiguration _configuration;
        private readonly CSPServerScriptProvider _serverScriptProvider;
        private readonly WeatherManager _weatherManager;
        private readonly SessionManager _sessionManager;
        private readonly EntryCarManager _entryCarManager;
        private readonly GeoParamsManager _geoParamsManager;

        public HttpController(ACServer server, CSPServerScriptProvider serverScriptProvider, WeatherManager weatherManager, SessionManager sessionManager, ACServerConfiguration configuration, EntryCarManager entryCarManager, GeoParamsManager geoParamsManager)
        {
            _server = server;
            _serverScriptProvider = serverScriptProvider;
            _weatherManager = weatherManager;
            _sessionManager = sessionManager;
            _configuration = configuration;
            _entryCarManager = entryCarManager;
            _geoParamsManager = geoParamsManager;
        }

        private string? IdFromGuid(string? guid)
        {
            if (guid != null)
            {
                using (var sha1 = SHA1.Create())
                {
                    var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes("antarcticfurseal" + guid));
                    StringBuilder sb = new StringBuilder();
                    foreach (byte b in hash)
                    {
                        sb.Append(b.ToString("x2"));
                    }

                    return sb.ToString();
                }
            }

            return null;
        }

        [HttpGet("/INFO")]
        public InfoResponse GetInfo()
        {
            InfoResponse responseObj = new InfoResponse()
            {
                Cars = _entryCarManager.EntryCars.Select(c => c.Model).Distinct(),
                Clients = _server.ConnectedCars.Count,
                Country = new string[] { _geoParamsManager.GeoParams.Country, _geoParamsManager.GeoParams.CountryCode },
                CPort = _configuration.Server.HttpPort,
                Durations = _configuration.Sessions.Select(c => c.IsTimedRace ? c.Time * 60 : c.Laps),
                Extra = _configuration.Server.HasExtraLap,
                Inverted = _configuration.Server.InvertedGridPositions,
                Ip = _geoParamsManager.GeoParams.Ip,
                MaxClients = _configuration.Server.MaxClients,
                Name = _configuration.Server.Name + (_configuration.Extra.EnableServerDetails ? " ℹ" + _configuration.Server.HttpPort : ""),
                Pass = !string.IsNullOrEmpty(_configuration.Server.Password),
                Pickup = true,
                Pit = false,
                Session = _sessionManager.CurrentSession.Configuration.Id,
                Port = _configuration.Server.UdpPort,
                SessionTypes = _configuration.Sessions.Select(s => s.Id + 1),
                Timed = false,
                TimeLeft = _sessionManager.CurrentSession.TimeLeftMilliseconds / 1000,
                TimeOfDay = (int)WeatherUtils.SunAngleFromTicks(_weatherManager.CurrentDateTime.TimeOfDay.TickOfDay),
                Timestamp = (int)_sessionManager.ServerTimeMilliseconds,
                TPort = _configuration.Server.TcpPort,
                Track = _configuration.Server.Track + (string.IsNullOrEmpty(_configuration.Server.TrackConfig) ? null : "-" + _configuration.Server.TrackConfig),
                PoweredBy = "AssettoServer " + _configuration.ServerVersion
            };

            return responseObj;
        }

        [HttpGet("/JSON{guid}")]
        public EntryListResponse GetEntryList(string guid)
        {
            guid = guid.Substring(1);
            bool isAdmin = !string.IsNullOrEmpty(guid) && _server.Admins.Contains(guid);

            EntryListResponse responseObj = new EntryListResponse
            {
                Cars = _entryCarManager.EntryCars.Select(ec => new EntryListResponseCar
                {
                    Model = ec.Model,
                    Skin = ec.Skin,
                    IsEntryList = ec.AiMode != AiMode.Fixed && (isAdmin || _configuration.Extra.AiParams.MaxPlayerCount == 0 ||
                                                                _server.ConnectedCars.Count < _configuration.Extra.AiParams.MaxPlayerCount),
                    DriverName = ec.Client?.Name,
                    DriverTeam = ec.Client?.Team,
                    IsConnected = ec.Client != null
                }).ToList(),
                Features = _server.Features
            };

            return responseObj;
        }

        [HttpGet("/api/details")]
        public DetailResponse GetDetails(string? guid)
        {
            bool isAdmin = !string.IsNullOrEmpty(guid) && _server.Admins.Contains(guid);

            DetailResponse responseObj = new DetailResponse()
            {
                Cars = _entryCarManager.EntryCars.Select(c => c.Model).Distinct(),
                Clients = _server.ConnectedCars.Count,
                Country = new string[] { _geoParamsManager.GeoParams.Country, _geoParamsManager.GeoParams.CountryCode },
                CPort = _configuration.Server.HttpPort,
                Durations = _configuration.Sessions.Select(c => c.IsTimedRace ? c.Time * 60 : c.Laps),
                Extra = _configuration.Server.HasExtraLap,
                Inverted = _configuration.Server.InvertedGridPositions,
                Ip = _geoParamsManager.GeoParams.Ip,
                MaxClients = _configuration.Server.MaxClients,
                Name = _configuration.Server.Name,
                Pass = !string.IsNullOrEmpty(_configuration.Server.Password),
                Pickup = true,
                Pit = false,
                Session = _sessionManager.CurrentSession.Configuration.Id,
                Port = _configuration.Server.UdpPort,
                SessionTypes = _configuration.Sessions.Select(s => s.Id + 1),
                Timed = false,
                TimeLeft = _sessionManager.CurrentSession.TimeLeftMilliseconds / 1000,
                TimeOfDay = (int)WeatherUtils.SunAngleFromTicks(_weatherManager.CurrentDateTime.TimeOfDay.TickOfDay),
                Timestamp = (int)_sessionManager.ServerTimeMilliseconds,
                TPort = _configuration.Server.TcpPort,
                Track = _configuration.Server.Track + (string.IsNullOrEmpty(_configuration.Server.TrackConfig) ? null : "-" + _configuration.Server.TrackConfig),
                Players = new DetailResponsePlayerList
                {
                    Cars = _entryCarManager.EntryCars.Select(ec => new DetailResponseCar
                    {
                        Model = ec.Model,
                        Skin = ec.Skin,
                        IsEntryList = ec.AiMode != AiMode.Fixed && (isAdmin || _configuration.Extra.AiParams.MaxPlayerCount == 0 ||
                                                                    _server.ConnectedCars.Count < _configuration.Extra.AiParams.MaxPlayerCount),
                        DriverName = ec.Client?.Name,
                        DriverTeam = ec.Client?.Team,
                        DriverNation = ec.Client?.NationCode,
                        IsConnected = ec.Client != null,
                        ID = IdFromGuid(ec.Client?.Guid)
                    }).ToList(),
                },
                Until = DateTimeOffset.Now.ToUnixTimeSeconds() + _sessionManager.CurrentSession.TimeLeftMilliseconds / 1000,
                Content = _configuration.ContentConfiguration,
                TrackBase = _configuration.Server.Track,
                City = _geoParamsManager.GeoParams.City,
                Frequency = _configuration.Server.RefreshRateHz,
                Assists = new DetailResponseAssists
                {
                    AbsState = _configuration.Server.ABSAllowed,
                    TcState = _configuration.Server.TractionControlAllowed,
                    FuelRate = (int)(_configuration.Server.FuelConsumptionRate * 100),
                    DamageMultiplier = (int)(_configuration.Server.MechanicalDamageRate * 100),
                    TyreWearRate = (int)(_configuration.Server.TyreConsumptionRate * 100),
                    AllowedTyresOut = _configuration.Server.AllowedTyresOutCount,
                    StabilityAllowed = _configuration.Server.StabilityAllowed,
                    AutoclutchAllowed = _configuration.Server.AutoClutchAllowed,
                    TyreBlanketsAllowed = _configuration.Server.AllowTyreBlankets,
                    ForceVirtualMirror = _configuration.Server.IsVirtualMirrorForced
                },
                WrappedPort = _configuration.Server.HttpPort,
                AmbientTemperature = _weatherManager.CurrentWeather.TemperatureAmbient,
                RoadTemperature = _weatherManager.CurrentWeather.TemperatureRoad,
                CurrentWeatherId = _weatherManager.CurrentWeather.Type.WeatherFxType == WeatherFxType.None ? _weatherManager.CurrentWeather.Type.Graphics : _weatherManager.CurrentWeather.Type.WeatherFxType.ToString(),
                WindSpeed = (int)_weatherManager.CurrentWeather.WindSpeed,
                WindDirection = _weatherManager.CurrentWeather.WindDirection,
                Description = _configuration.Extra.ServerDescription,
                Grip = _weatherManager.CurrentWeather.TrackGrip * 100,
                Features = _server.Features,
                PoweredBy = "AssettoServer " + _configuration.ServerVersion
            };
            
            return responseObj;
        }

        [HttpGet("/api/scripts/{scriptId:int}")]
        public ActionResult<string> GetScript(int scriptId)
        {
            if (scriptId < _serverScriptProvider.Scripts.Count)
            {
                return _serverScriptProvider.Scripts[scriptId];
            }

            return NotFound();
        }
    }
}
