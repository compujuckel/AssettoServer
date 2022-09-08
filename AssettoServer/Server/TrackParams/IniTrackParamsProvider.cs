using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using IniParser;
using Serilog;

namespace AssettoServer.Server.TrackParams;

public class IniTrackParamsProvider : ITrackParamsProvider
{
    private const string TrackParamsPath = "cfg/data_track_params.ini";

    private const string RemoteTrackParamsUrl =
        "https://raw.githubusercontent.com/ac-custom-shaders-patch/acc-extension-config/master/config/data_track_params.ini";
        
    private readonly HttpClient _httpClient;
        
    public IniTrackParamsProvider()
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
    }

    public async Task Initialize()
    {
        if (!File.Exists(TrackParamsPath))
        {
            Log.Information("{Path} not found, downloading from GitHub...", TrackParamsPath);

            try
            {
                var response = await _httpClient.GetAsync(RemoteTrackParamsUrl);

                if (!response.IsSuccessStatusCode)
                {
                    Log.Error("Could not get track params from {TrackParamsUrl} ({StatusCode})", RemoteTrackParamsUrl, response.StatusCode);
                }
                else
                {
                    await using var file = File.Create(TrackParamsPath);
                    var responseStream = await response.Content.ReadAsStreamAsync();
                    await responseStream.CopyToAsync(file);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not get track params from {TrackParamsUrl}", RemoteTrackParamsUrl);
            }
        }
    }
        
    public TrackParams? GetParamsForTrack(string track)
    {
        if (!File.Exists(TrackParamsPath)) return null;
            
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
