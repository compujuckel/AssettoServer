using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssettoServer.Server.Configuration;
using AssettoServer.Shared.Network.Http.Responses;
using Serilog;

namespace AssettoServer.Server.CMContentProviders;

public class DefaultCMContentProvider : ICMContentProvider
{
    private readonly ACServerConfiguration _configuration;
    private readonly string _contentPath;
    private readonly string _cachePath;

    private Dictionary<Tuple<string, string>, string> _cmIndex = [];

    public DefaultCMContentProvider(ACServerConfiguration configuration)
    {
        _configuration = configuration;
        _contentPath = "content";
        _cachePath = Path.Join(_contentPath, "cache");
    }

    public ValueTask<CMContentConfiguration?> GetContentAsync(ulong guid)
    {
        return ValueTask.FromResult(_configuration.ContentConfiguration);
    }

    public void Initialize()
    {
        if (_configuration.ContentConfiguration == null) return;
        
        Log.Information("Preparing Content for CM Integration, this could take a moment");

        if (_configuration.ContentConfiguration.Track != null)
        {
            var trackEntry = _configuration.Server.Track.Split('/').Last();
            PrepareTrackDownload(trackEntry);
        }

        if (_configuration.ContentConfiguration.Cars != null)
        {
            foreach (var car in _configuration.ContentConfiguration.Cars)
            {
                PrepareCarDownload(car.Key);
            }
        }
        
        Log.Information("Content preparation finished");
    }

    private void PrepareCarDownload(string carId)
    {
        if (_configuration.ContentConfiguration!.Cars!.TryGetValue(carId, out var car))
        {
            if (car.Url != null) return;
        }
        else return;

        if (car.File == null) return;
        string zipPath = car.File;

        if (Path.Exists(zipPath))
            _cmIndex.Add(new ("cars", carId), zipPath);

        if (car.Skins == null) return;
        foreach (var (skinId, skin) in car.Skins)
            PrepareSkinDownload(carId, skinId, skin);
    }
    
    private void PrepareSkinDownload(string parentId, string skinId, CMContentEntry skin)
    {
        if (skin.Url != null) return;
        if (skin.File == null) return;
        
        string zipPath = skin.File;
        
        if (Path.Exists(zipPath))
            _cmIndex.Add(new ("skins",$"{parentId}/{skinId}"), zipPath);
    }
    
    private void PrepareTrackDownload(string trackId)
    {
        CMContentEntryVersionized track = _configuration.ContentConfiguration!.Track!;
        
        if (track.Url != null) return;

        if (track.File == null) return;
        string zipPath = track.File;
        
        if (Path.Exists(zipPath))
            _cmIndex.Add(new ("tracks",trackId), zipPath);
    }

    public bool TryGetZipPath(string type, string entry, out string? path)
    {
        return _cmIndex.TryGetValue(new (type, entry), out path);
    }
}
