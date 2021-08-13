using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using IniParser;
using IniParser.Model;
using Serilog;
using Serilog.Core;

namespace AssettoServer.Server.TrackParams
{
    public class IniTrackParamsProvider : ITrackParamsProvider
    {
        private const string TrackParamsPath = "cfg/data_track_params.ini";

        private const string RemoteTrackParamsUrl =
            "https://raw.githubusercontent.com/ac-custom-shaders-patch/acc-extension-config/master/config/data_track_params.ini";
        
        private readonly HttpClient _httpClient;
        
        public IniTrackParamsProvider()
        {
            _httpClient = new HttpClient();
        }

        public async Task Initialize()
        {
            if (!File.Exists(TrackParamsPath))
            {
                Log.Information("{0} not found, downloading from GitHub...", TrackParamsPath);
                var response = await _httpClient.GetAsync(RemoteTrackParamsUrl);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception(
                        "Could not get track params from GitHub. Please download or create them manually.");
                }

                await using var file = File.Create(TrackParamsPath);
                var responseStream = await response.Content.ReadAsStreamAsync();
                await responseStream.CopyToAsync(file);
            }
        }
        
        public TrackParams GetParamsForTrack(string track)
        {
            var cleanTrack = track.Substring(track.LastIndexOf('/') + 1);
            
            var parser = new FileIniDataParser();
            var data = parser.ReadFile(TrackParamsPath);

            if (data.Sections.ContainsSection(cleanTrack))
            {
                return new TrackParams()
                {
                    Latitude = double.Parse(data[cleanTrack]["LATITUDE"]),
                    Longitude = double.Parse(data[cleanTrack]["LONGITUDE"]),
                    Name = data[cleanTrack]["NAME"],
                    Timezone = data[cleanTrack]["TIMEZONE"]
                };
            }

            return null;
        }
    }
}