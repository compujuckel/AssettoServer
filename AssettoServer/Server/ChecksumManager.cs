using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using AssettoServer.Server.Checksum;
using AssettoServer.Server.Configuration;
using AssettoServer.Shared.Checksum;
using Serilog;

namespace AssettoServer.Server;

public class ChecksumManager
{
    public IReadOnlyDictionary<string, byte[]> TrackChecksums { get; private set; } = null!;
    public IReadOnlyDictionary<string, Dictionary<string, byte[]>> CarChecksums { get; private set; } = null!;
    public IReadOnlyDictionary<string, byte[]> AdditionalCarChecksums { get; private set; } = null!;

    private readonly ACServerConfiguration _configuration;
    private readonly EntryCarManager _entryCarManager;
    private readonly ChecksumDataProvider _checksumDataProvider;
    private ChecksumsFile _preloadedChecksums = null!;
    private Dictionary<string, byte[]> _trackChecksumData = null!;
    private Dictionary<string, byte[]> _additionalCarChecksumData = null!;
    
    public ChecksumManager(
        ACServerConfiguration configuration,
        EntryCarManager entryCarManager,
        ChecksumDataProvider checksumDataProvider)
    {
        _configuration = configuration;
        _entryCarManager = entryCarManager;
        _checksumDataProvider = checksumDataProvider;
    }
    
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _preloadedChecksums = await _checksumDataProvider.LoadAsync(cancellationToken);

        CalculateTrackChecksums(_configuration.Server.Track, _configuration.Server.TrackConfig);
        AddPreloadedTrackChecksums(_configuration.Server.TrackConfig);
        Log.Information("Initialized {Count} track checksums", TrackChecksums.Count);

        var carModels = _entryCarManager.EntryCars.Select(car => car.Model).Distinct().ToList();
        CalculateCarChecksums(carModels, _configuration.Extra.EnableAlternativeCarChecksums);
        AddPreloadedCarChecksums(carModels, _configuration.Extra.EnableAlternativeCarChecksums);
        Log.Information("Initialized {Count} car checksums", CarChecksums.Select(car => car.Value.Count).Sum());

        var modelsWithoutChecksums = CarChecksums.Where(c => c.Value.Count == 0).Select(c => c.Key).ToList();
        if (modelsWithoutChecksums.Count > 0)
        {
            string models = string.Join(", ", modelsWithoutChecksums);

            if (_configuration.Extra.IgnoreConfigurationErrors.MissingCarChecksums)
            {
                Log.Warning("No data.acd found for {CarModels}. This will allow players to cheat using modified data. More info: https://assettoserver.org/docs/common-configuration-errors#missing-car-checksums", models);
            }
            else
            {
                throw new ConfigurationException($"No data.acd found for {models}. This will allow players to cheat using modified data. More info: https://assettoserver.org/docs/common-configuration-errors#missing-car-checksums")
                {
                    HelpLink = "https://assettoserver.org/docs/common-configuration-errors#missing-car-checksums"
                };
            }
        }
    }

    private void AddPreloadedTrackChecksums(string trackConfig)
    {
        var systemSurfaces = ChecksumsFile.Find(_preloadedChecksums.Other, "system/data/surfaces.ini");
        if (systemSurfaces?.MD5 is { } systemSurfacesMd5)
            AddPreloadedChecksum(_trackChecksumData, "system/data/surfaces.ini", systemSurfacesMd5);

        if (!_preloadedChecksums.TryGetTrack(_configuration.CSPTrackOptions.Track, trackConfig,
                out var track, out var trackLayout) || track == null)
            return;

        var virtualTrackPath = $"content/tracks/{_configuration.Server.Track}";
        bool surfaceFix = _configuration.CSPTrackOptions.MinimumCSPVersion.HasValue;
        AddPreloadedFiles(_trackChecksumData, virtualTrackPath, track, surfaceFix);
        if (trackLayout != null)
            AddPreloadedFiles(_trackChecksumData, virtualTrackPath, trackLayout, surfaceFix);
    }

    private static void AddPreloadedFiles(
        Dictionary<string, byte[]> checksums,
        string virtualPath,
        TrackChecksumEntry entry,
        bool surfaceFix)
    {
        foreach (var (path, checksum) in entry.Files)
        {
            if (checksum.MD5 is not { } md5)
                continue;

            string normalizedPath = path.Replace('\\', '/').TrimStart('/');
            AddPreloadedChecksum(checksums, $"{virtualPath}/{normalizedPath}", md5);
        }

        foreach (var (path, variants) in entry.Surfaces)
        {
            string normalizedPath = path.Replace('\\', '/').TrimStart('/');
            var checksum = surfaceFix && !normalizedPath.Equals("surfaces.ini", StringComparison.OrdinalIgnoreCase)
                ? variants.Csp
                : variants.Vanilla;
            if (checksum?.MD5 is not { } md5)
                continue;

            AddPreloadedChecksum(checksums, $"{virtualPath}/{normalizedPath}", md5);
        }
    }

    private void AddPreloadedCarChecksums(IEnumerable<string> cars, bool allowAlternatives)
    {
        foreach (string car in cars)
        {
            var carChecksums = ChecksumsFile.Find(_preloadedChecksums.Cars, car);
            if (carChecksums == null)
                continue;

            foreach (var (path, checksum) in carChecksums.Files)
            {
                string normalizedPath = path.Replace('\\', '/').TrimStart('/');
                string fileName = Path.GetFileName(normalizedPath);
                string virtualPath = $"content/cars/{car}/{normalizedPath}";

                var md5 = checksum.MD5;
                if (fileName.Equals("collider.kn5", StringComparison.OrdinalIgnoreCase) && md5.HasValue)
                    AddPreloadedChecksum(_additionalCarChecksumData, virtualPath, md5.Value);

                if (!md5.HasValue || !fileName.StartsWith("data", StringComparison.OrdinalIgnoreCase)
                    || !fileName.EndsWith(".acd", StringComparison.OrdinalIgnoreCase)
                    || (!allowAlternatives && !fileName.Equals("data.acd", StringComparison.OrdinalIgnoreCase)))
                    continue;

                AddPreloadedChecksum(CarChecksums[car], virtualPath, md5.Value);
            }
        }
    }

    internal static void AddPreloadedChecksum(Dictionary<string, byte[]> checksums, string path, Md5Checksum checksum)
    {
        string normalizedPath = path.Replace('\\', '/');
        if (checksums.Keys.Any(existing => string.Equals(
                existing.Replace('\\', '/'), normalizedPath, StringComparison.OrdinalIgnoreCase)))
            return;

        checksums.Add(normalizedPath, checksum.ToArray());
    }

    public List<KeyValuePair<string, byte[]>> GetChecksumsForHandshake(string car)
    {
        return TrackChecksums
            .Concat(AdditionalCarChecksums.Where(c => c.Key.StartsWith($"content/cars/{car}/")))
            .ToList();
    }

    private void CalculateTrackChecksums(string track, string trackConfig)
    {
        var dict = new Dictionary<string, byte[]>();
        var surfaceFix = _configuration.CSPTrackOptions.MinimumCSPVersion.HasValue;
        
        AddChecksum(dict, "system/data/surfaces.ini");

        var realTrackPath = $"content/tracks/{track}";
        var virtualTrackPath = realTrackPath;
        if (!Directory.Exists(realTrackPath))
        {
            realTrackPath = $"content/tracks/{_configuration.CSPTrackOptions.Track}";
        }

        if (string.IsNullOrEmpty(trackConfig))
        {
            AddChecksumVirtualPath(dict, realTrackPath, virtualTrackPath, "data/surfaces.ini", surfaceFix);
            AddChecksumVirtualPath(dict, realTrackPath, virtualTrackPath, "models.ini");
        }
        else
        {
            AddChecksumVirtualPath(dict, realTrackPath, virtualTrackPath, $"{trackConfig}/data/surfaces.ini", surfaceFix);
            AddChecksumVirtualPath(dict, realTrackPath, virtualTrackPath, $"models_{trackConfig}.ini", surfaceFix);
        }
        
        ChecksumDirectory(dict, realTrackPath, virtualTrackPath);

        TrackChecksums = dict;
        _trackChecksumData = dict;
    }

    private void CalculateCarChecksums(IEnumerable<string> cars, bool allowAlternatives)
    {
        var carDataChecksums = new Dictionary<string, Dictionary<string, byte[]>>();
        var additionalChecksums = new Dictionary<string, byte[]>();

        foreach (string car in cars)
        {
            string carFolder = $"content/cars/{car}";

            AddChecksum(additionalChecksums, $"{carFolder}/collider.kn5");
            
            var checksums = new Dictionary<string, byte[]>();
            if (allowAlternatives && Directory.Exists(carFolder))
            {
                foreach (string file in Directory.EnumerateFiles(carFolder, "data*.acd"))
                {
                    if (TryCreateChecksum(file, out byte[]? checksum))
                    {
                        checksums.Add(file, checksum);
                        Log.Debug("Added checksum for {Path}", file);
                    }
                }
            }
            else
            {
                var acdPath = Path.Join(carFolder, "data.acd");
                if (TryCreateChecksum(acdPath, out byte[]? checksum))
                {
                    checksums.Add(acdPath, checksum);
                    Log.Debug("Added checksum for {Path}", car);
                }
            }

            carDataChecksums.Add(car, checksums);
        }

        CarChecksums = carDataChecksums;
        AdditionalCarChecksums = additionalChecksums;
        _additionalCarChecksumData = additionalChecksums;
    }

    private static bool TryCreateChecksum(string filePath, [MaybeNullWhen(false)] out byte[] checksum, bool surfaceFix = false)
    {
        if (File.Exists(filePath))
        {
            if (surfaceFix)
            {
                var bytes = File.ReadAllBytes(filePath);
                var firstSurface = MemoryExtensions.IndexOf(bytes, "SURFACE_0"u8);
                if (firstSurface > 0)
                {
                    "CSP"u8.CopyTo(bytes.AsSpan(firstSurface, 3));
                }

                checksum = MD5.HashData(bytes);
            }
            else
            {
                using var fileStream = File.OpenRead(filePath);
                checksum = MD5.HashData(fileStream);
            }

            return true;
        }

        checksum = null;
        return false;
    }

    private static void AddChecksumVirtualPath(Dictionary<string, byte[]> dict, string path, string virtualPath, string file, bool surfaceFix = false) 
        => AddChecksum(dict, $"{path}/{file}", $"{virtualPath}/{file}", surfaceFix); 

    private static void AddChecksum(Dictionary<string, byte[]> dict, string filePath, string? name = null, bool surfaceFix = false)
    {
        if (TryCreateChecksum(filePath, out byte[]? checksum, surfaceFix))
        {
            dict.Add(name ?? filePath, checksum);
            Log.Debug("Added checksum for {Path}", name ?? filePath);
        }
    }
    
    private static void ChecksumDirectory(Dictionary<string, byte[]> dict, string path, string? virtualPath = null)
    {
        if (!Directory.Exists(path))
            return;

        virtualPath ??= path;
        
        foreach (var file in Directory.GetFiles(path))
        {
            var name = Path.GetFileName(file);
            var virtualName = $"{virtualPath}/{name.Replace("\\", "/")}";
            
            if (name == "surfaces.ini" || name.EndsWith(".kn5"))
            {
                AddChecksum(dict, file, virtualName);
            }
        }
    }
}
