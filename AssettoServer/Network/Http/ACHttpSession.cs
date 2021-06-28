using AssettoServer.Server;
using NetCoreServer;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssettoServer.Network.Http
{
    class ACHttpSession : HttpSession
    {
        public ACServer ACServer { get; }

        public ACHttpSession(ACServer acServer, HttpServer server) : base(server)
        { 
            ACServer = acServer;
        }

        protected override void OnReceivedRequest(HttpRequest request)
        {
            if(request.Method == "GET")
            {
                string requestUrl = request.Url;
                string responseString = null;

                if (requestUrl.Equals("/INFO", StringComparison.OrdinalIgnoreCase))
                {
                    InfoResponse responseObj = new InfoResponse()
                    {
                        Cars = ACServer.Configuration.EntryCars.Select(c => c.Model).Distinct(),
                        Clients = ACServer.ConnectedCars.Count,
                        Country = new string[] { "na", "na" },
                        CPort = ACServer.Configuration.HttpPort,
                        Durations = ACServer.Configuration.Sessions.Select(c => c.Type == 3 ? c.Laps : c.Time),
                        Extra = ACServer.Configuration.HasExtraLap,
                        Inverted = ACServer.Configuration.InvertedGridPositions,
                        Ip = "",
                        MaxClients = ACServer.Configuration.MaxClients,
                        Name = ACServer.Configuration.Name,
                        Pass = !string.IsNullOrEmpty(ACServer.Configuration.Password),
                        Pickup = true,
                        Pit = false,
                        Session = ACServer.CurrentSession.Id,
                        Port = ACServer.Configuration.UdpPort,
                        SessionTypes = ACServer.Configuration.Sessions.Select(s => s.Id + 1),
                        Timed = false,
                        TimeLeft = (int)ACServer.CurrentSession.TimeLeft.TotalSeconds,
                        TimeOfDay = (int)ACServer.Configuration.SunAngle,
                        Timestamp = ACServer.CurrentTime,
                        TPort = ACServer.Configuration.TcpPort,
                        Track = ACServer.Configuration.Track + (string.IsNullOrEmpty(ACServer.Configuration.TrackConfig) ? null : "-" + ACServer.Configuration.TrackConfig)
                    };

                    responseString = JsonConvert.SerializeObject(responseObj, Formatting.None, new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() });
                }
                else if (requestUrl.StartsWith("/JSON", StringComparison.OrdinalIgnoreCase))
                {
                    EntryListResponse responseObj = new EntryListResponse
                    {
                        Cars = ACServer.EntryCars.Select(ec => new EntryListResponseCar { Model = ec.Model, Skin = ec.Skin, IsEntryList = true, DriverName = ec?.Client?.Name, DriverTeam = ec?.Client?.Team, IsConnected = ec.Client != null }).ToList(),
                        
                    };

                    var features = new List<string>();
                    if (ACServer.Configuration.Extra.UseSteamAuth)
                        features.Add("STEAM_TICKET");

                    features.Add("SPECTATING_AWARE");
                    features.Add("LOWER_CLIENTS_SENDING_RATE");
                    features.Add("WEATHERFX_V1");
                    features.Add("CLIENTS_EXCHANGE_V1");

                    responseObj.Features = features;

                    responseString = JsonConvert.SerializeObject(responseObj);
                }

                if (responseString != null)
                {
                    SendResponse(Response.MakeGetResponse(responseString, "application/json"));
                }
                else
                    SendResponse(Response.MakeErrorResponse());
            }
        }
    }
}
