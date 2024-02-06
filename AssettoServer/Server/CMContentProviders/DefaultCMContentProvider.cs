using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AssettoServer.Server.Configuration;
using AssettoServer.Shared.Network.Http.Responses;

namespace AssettoServer.Server.CMContentProviders;

public class DefaultCMContentProvider : ICMContentProvider
{
    private readonly ACServerConfiguration _configuration;

    private readonly Dictionary<Tuple<string, string>, string> _cmIndex = [];

    public DefaultCMContentProvider(ACServerConfiguration configuration)
    {
        _configuration = configuration;
    }

    public ValueTask<CMContentConfiguration?> GetContentAsync(ulong guid)
    {
        return ValueTask.FromResult(_configuration.ContentConfiguration);
    }

    public void Initialize()
    {
        if (_configuration.ContentConfiguration == null) return;

        if (_configuration.ContentConfiguration.Track != null)
        {
            var trackEntry = _configuration.Server.Track.Split('/').Last();
            PrepareTrackDownload(trackEntry, _configuration.ContentConfiguration.Track);
        }

        if (_configuration.ContentConfiguration.Cars != null)
        {
            foreach (var car in _configuration.ContentConfiguration.Cars)
            {
                PrepareCarDownload(car.Key, car.Value);
            }
        }
    }

    private void PrepareCarDownload(string carId, CMContentEntryCar car)
    {
        if (car.Url != null) return;

        if (car.File == null) return;
        var zipPath = car.File;

        if (Path.Exists(zipPath))
            _cmIndex.Add(new("cars", carId), zipPath);

        if (car.Skins == null) return;
        foreach (var (skinId, skin) in car.Skins)
            PrepareSkinDownload(carId, skinId, skin);
    }
    
    private void PrepareSkinDownload(string parentId, string skinId, CMContentEntry skin)
    {
        if (skin.Url != null) return;
        if (skin.File == null) return;
        
        var zipPath = skin.File;
        
        if (Path.Exists(zipPath))
            _cmIndex.Add(new ("skins",$"{parentId}/{skinId}"), zipPath);
    }
    
    private void PrepareTrackDownload(string trackId, CMContentEntryVersionized track)
    {
        if (track.Url != null) return;

        if (track.File == null) return;
        var zipPath = track.File;
        
        if (Path.Exists(zipPath))
            _cmIndex.Add(new ("tracks",trackId), zipPath);
    }

    public bool TryGetZipPath(string type, string entry, [NotNullWhen(true)] out string? path)
    {
        if (_cmIndex.TryGetValue(new (type, entry), out var result))
        {
            path = result;
            return true;
        }

        path = default;
        return false;
    }
}
