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
    private readonly CMContentConfiguration? _contentConfiguration;

    private readonly Dictionary<string, string> _carIndex = [];
    private readonly Dictionary<string, string> _skinIndex = [];
    private readonly Dictionary<string, string> _trackIndex = [];

    public DefaultCMContentProvider(ACServerConfiguration configuration)
    {
        _configuration = configuration;
        _contentConfiguration = configuration.ContentConfiguration;

        if (_contentConfiguration?.Track != null)
            _contentConfiguration.Track.Direct = _contentConfiguration?.Track?.File != null ? true : null;

        if (_contentConfiguration?.Cars == null) return;
        foreach (var (_, car) in _contentConfiguration.Cars)
        {
            car.Direct = car.File != null ? true : null;

            if (car.Skins == null) continue;
            foreach (var (_, skin) in car.Skins)
            {
                skin.Direct = skin.File != null ? true : null;
            }
        }
    }

    public ValueTask<CMContentConfiguration?> GetContentAsync(ulong guid)
    {
        return ValueTask.FromResult(_contentConfiguration);
    }

    public void Initialize()
    {
        if (_contentConfiguration == null) return;

        if (_contentConfiguration.Track != null)
        {
            var trackEntry = _configuration.Server.Track.Split('/').Last();
            PrepareTrackDownload(trackEntry, _contentConfiguration.Track);
        }

        if (_contentConfiguration.Cars == null) return;
        foreach (var car in _contentConfiguration.Cars)
        {
            PrepareCarDownload(car.Key, car.Value);
        }
    }

    private void PrepareCarDownload(string carId, CMContentEntryCar car)
    {
        if (car.Direct != true) return;
        
        var zipPath = car.File;

        if (Path.Exists(zipPath))
            _carIndex.Add(carId, zipPath);

        if (car.Skins == null) return;
        foreach (var (skinId, skin) in car.Skins)
            PrepareSkinDownload(carId, skinId, skin);
    }
    
    private void PrepareSkinDownload(string parentId, string skinId, CMContentEntry skin)
    {
        if (skin.Direct != true) return;
        
        var zipPath = skin.File;
        
        if (Path.Exists(zipPath))
            _skinIndex.Add($"{parentId}/{skinId}", zipPath);
    }
    
    private void PrepareTrackDownload(string trackId, CMContentEntryVersionized track)
    {
        if (track.Direct != true) return;
        
        var zipPath = track.File;
        
        if (Path.Exists(zipPath))
            _trackIndex.Add(trackId, zipPath);
    }

    public bool TryGetZipPath(string type, string entry, [NotNullWhen(true)] out string? path)
    {
        switch (type)
        {
            case "cars":
                return _carIndex.TryGetValue(entry, out path);
            case "skins":
                return _skinIndex.TryGetValue(entry, out path);
            case "tracks":
                return _trackIndex.TryGetValue(entry, out path);
        }

        path = default;
        return false;
    }
}
