using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AssettoServer.Network.Http.Responses;
using AssettoServer.Server;
using AssettoServer.Server.Weather;
using Microsoft.AspNetCore.Mvc;

namespace AssettoServer.Network.Http
{
    [ApiController]
    public class HttpController : ControllerBase
    {

        private readonly ACServer _server;

        public HttpController(ACServer server)
        {
            _server = server;
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
                Cars = _server.EntryCars.Select(c => c.Model).Distinct(),
                Clients = _server.ConnectedCars.Count,
                Country = new string[] { _server.GeoParams.Country, _server.GeoParams.CountryCode },
                CPort = _server.Configuration.Server.HttpPort,
                Durations = _server.Configuration.Sessions.Select(c => c.IsTimedRace ? c.Time * 60 : c.Laps),
                Extra = _server.Configuration.Server.HasExtraLap,
                Inverted = _server.Configuration.Server.InvertedGridPositions,
                Ip = _server.GeoParams.Ip,
                MaxClients = _server.Configuration.Server.MaxClients,
                Name = _server.Configuration.Server.Name + (_server.Configuration.Extra.EnableServerDetails ? " ℹ" + _server.Configuration.Server.HttpPort : ""),
                Pass = !string.IsNullOrEmpty(_server.Configuration.Server.Password),
                Pickup = true,
                Pit = false,
                Session = _server.CurrentSession.Configuration.Id,
                Port = _server.Configuration.Server.UdpPort,
                SessionTypes = _server.Configuration.Sessions.Select(s => s.Id + 1),
                Timed = false,
                TimeLeft = _server.CurrentSession.TimeLeftTicks / 1000,
                TimeOfDay = (int)WeatherUtils.SunAngleFromTicks(_server.CurrentDateTime.TimeOfDay.TickOfDay),
                Timestamp = _server.CurrentTime,
                TPort = _server.Configuration.Server.TcpPort,
                Track = _server.Configuration.Server.Track + (string.IsNullOrEmpty(_server.Configuration.Server.TrackConfig) ? null : "-" + _server.Configuration.Server.TrackConfig),
                PoweredBy = "AssettoServer " + _server.Configuration.ServerVersion
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
                Cars = _server.EntryCars.Select(ec => new EntryListResponseCar
                {
                    Model = ec.Model,
                    Skin = ec.Skin,
                    IsEntryList = ec.AiMode != AiMode.Fixed && (isAdmin || _server.Configuration.Extra.AiParams.MaxPlayerCount == 0 ||
                                                                _server.ConnectedCars.Count < _server.Configuration.Extra.AiParams.MaxPlayerCount),
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
                Cars = _server.EntryCars.Select(c => c.Model).Distinct(),
                Clients = _server.ConnectedCars.Count,
                Country = new string[] { _server.GeoParams.Country, _server.GeoParams.CountryCode },
                CPort = _server.Configuration.Server.HttpPort,
                Durations = _server.Configuration.Sessions.Select(c => c.IsTimedRace ? c.Time * 60 : c.Laps),
                Extra = _server.Configuration.Server.HasExtraLap,
                Inverted = _server.Configuration.Server.InvertedGridPositions,
                Ip = _server.GeoParams.Ip,
                MaxClients = _server.Configuration.Server.MaxClients,
                Name = _server.Configuration.Server.Name,
                Pass = !string.IsNullOrEmpty(_server.Configuration.Server.Password),
                Pickup = true,
                Pit = false,
                Session = _server.CurrentSession.Configuration.Id,
                Port = _server.Configuration.Server.UdpPort,
                SessionTypes = _server.Configuration.Sessions.Select(s => s.Id + 1),
                Timed = false,
                TimeLeft = _server.CurrentSession.TimeLeftTicks / 1000,
                TimeOfDay = (int)WeatherUtils.SunAngleFromTicks(_server.CurrentDateTime.TimeOfDay.TickOfDay),
                Timestamp = _server.CurrentTime,
                TPort = _server.Configuration.Server.TcpPort,
                Track = _server.Configuration.Server.Track + (string.IsNullOrEmpty(_server.Configuration.Server.TrackConfig) ? null : "-" + _server.Configuration.Server.TrackConfig),
                Players = new DetailResponsePlayerList
                {
                    Cars = _server.EntryCars.Select(ec => new DetailResponseCar
                    {
                        Model = ec.Model,
                        Skin = ec.Skin,
                        IsEntryList = ec.AiMode != AiMode.Fixed && (isAdmin || _server.Configuration.Extra.AiParams.MaxPlayerCount == 0 ||
                                                                    _server.ConnectedCars.Count < _server.Configuration.Extra.AiParams.MaxPlayerCount),
                        DriverName = ec.Client?.Name,
                        DriverTeam = ec.Client?.Team,
                        DriverNation = ec.Client?.NationCode,
                        IsConnected = ec.Client != null,
                        ID = IdFromGuid(ec.Client?.Guid)
                    }).ToList(),
                },
                Until = DateTimeOffset.Now.ToUnixTimeSeconds() + _server.CurrentSession.TimeLeftTicks / 1000,
                Content = _server.Configuration.ContentConfiguration,
                TrackBase = _server.Configuration.Server.Track,
                City = _server.GeoParams.City,
                Frequency = _server.Configuration.Server.RefreshRateHz,
                Assists = new DetailResponseAssists
                {
                    AbsState = _server.Configuration.Server.ABSAllowed,
                    TcState = _server.Configuration.Server.TractionControlAllowed,
                    FuelRate = (int)(_server.Configuration.Server.FuelConsumptionRate * 100),
                    DamageMultiplier = (int)(_server.Configuration.Server.MechanicalDamageRate * 100),
                    TyreWearRate = (int)(_server.Configuration.Server.TyreConsumptionRate * 100),
                    AllowedTyresOut = _server.Configuration.Server.AllowedTyresOutCount,
                    StabilityAllowed = _server.Configuration.Server.StabilityAllowed,
                    AutoclutchAllowed = _server.Configuration.Server.AutoClutchAllowed,
                    TyreBlanketsAllowed = _server.Configuration.Server.AllowTyreBlankets,
                    ForceVirtualMirror = _server.Configuration.Server.IsVirtualMirrorForced
                },
                WrappedPort = _server.Configuration.Server.HttpPort,
                AmbientTemperature = _server.CurrentWeather.TemperatureAmbient,
                RoadTemperature = _server.CurrentWeather.TemperatureRoad,
                CurrentWeatherId = _server.CurrentWeather.Type.WeatherFxType == WeatherFxType.None ? _server.CurrentWeather.Type.Graphics : _server.CurrentWeather.Type.WeatherFxType.ToString(),
                WindSpeed = (int)_server.CurrentWeather.WindSpeed,
                WindDirection = _server.CurrentWeather.WindDirection,
                Description = _server.Configuration.Extra.ServerDescription,
                Grip = _server.CurrentWeather.TrackGrip * 100,
                Features = _server.Features,
                PoweredBy = "AssettoServer " + _server.Configuration.ServerVersion
            };
            
            return responseObj;
        }

        [HttpGet("/api/scripts/{scriptId:int}")]
        public ActionResult<string> GetScript(int scriptId)
        {
            if (scriptId < _server.CSPLuaClientScriptProvider.Scripts.Count)
            {
                return _server.CSPLuaClientScriptProvider.Scripts[scriptId];
            }

            return NotFound();
        }
    }
}
