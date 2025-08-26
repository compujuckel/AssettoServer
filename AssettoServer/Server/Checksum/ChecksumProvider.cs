using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Serilog;
using YamlDotNet.Serialization;

namespace AssettoServer.Server.Checksum;

public class ChecksumProvider
{
    private const string ChecksumsPath = "cfg/data_checksums.yaml";

    private const string RemoteChecksumsUrl =
        "https://raw.githubusercontent.com/compujuckel/AssettoServer/master/AssettoServer/Assets/data_checksums.yaml";
        
    private readonly HttpClient _httpClient;
    private ChecksumsFile? _checksums;

    public ChecksumProvider()
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
    }

    public async Task InitializeAsync()
    {
        if (!File.Exists(ChecksumsPath))
        {
            Log.Information("{Path} not found, downloading from GitHub...", ChecksumsPath);

            try
            {
                var response = await _httpClient.GetAsync(RemoteChecksumsUrl);

                if (!response.IsSuccessStatusCode)
                {
                    Log.Error("Could not get checksums from {TrackParamsUrl} ({StatusCode})", RemoteChecksumsUrl, response.StatusCode);
                }
                else
                {
                    await using var file = File.Create(ChecksumsPath);
                    var responseStream = await response.Content.ReadAsStreamAsync();
                    await responseStream.CopyToAsync(file);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not get checksums from {TrackParamsUrl}", RemoteChecksumsUrl);
            }
        }
        
        var deserializer = new DeserializerBuilder().Build();
        using var checksumsFile = File.OpenText(ChecksumsPath);
        _checksums = deserializer.Deserialize<ChecksumsFile>(checksumsFile);
    }
        
    public TrackLayoutChecksum? GetChecksumsForTrack(string track, string? layout)
    {
        if (_checksums == null) return null;
            
        var cleanTrack = track.Substring(track.LastIndexOf('/') + 1);

        if (_checksums.Tracks.TryGetValue(cleanTrack, out var checksum))
        {
            if (string.IsNullOrEmpty(layout))
            {
                return checksum.Default;
            }

            return checksum.Layouts?.GetValueOrDefault(layout);
        }

        return null;
    }
        
    public ChecksumFileList? GetChecksumsForCar(string car)
    {
        if (_checksums == null) return null;
        
        if (_checksums.Cars.TryGetValue(car, out var checksum))
        {
            return checksum;
        }

        return null;
    }
        
    public ChecksumItem? GetChecksumsForOther(string car)
    {
        if (_checksums == null) return null;
        
        if (_checksums.Other.TryGetValue(car, out var checksum))
        {
            return checksum;
        }

        return null;
    }
}
