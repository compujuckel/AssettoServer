using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using AssettoServer.Server.Configuration;
using Serilog;

namespace AssettoServer.Server.Checksum;

public class ChecksumManager
{
    public IReadOnlyDictionary<string, byte[]> TrackChecksums { get; private set; } = null!;
    public IReadOnlyDictionary<string, Dictionary<string, byte[]>> CarChecksums { get; private set; } = null!;
    public IReadOnlyDictionary<string, byte[]> AdditionalCarChecksums { get; private set; } = null!;

    private readonly ACServerConfiguration _configuration;
    private readonly EntryCarManager _entryCarManager;
    private readonly ChecksumProvider _checksumProvider;

    public ChecksumManager(ACServerConfiguration configuration,
        EntryCarManager entryCarManager,
        ChecksumProvider checksumProvider)
    {
        _configuration = configuration;
        _entryCarManager = entryCarManager;
        _checksumProvider = checksumProvider;
    }
    
    public async Task Initialize()
    {
        await _checksumProvider.InitializeAsync();
        CalculateTrackChecksums(_configuration.Server.Track, _configuration.Server.TrackConfig);
        Log.Information("Initialized {Count} track checksums", TrackChecksums.Count);

        var carModels = _entryCarManager.EntryCars.Select(car => car.Model).Distinct().ToList();
        CalculateCarChecksums(carModels, _configuration.Extra.EnableAlternativeCarChecksums);
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
        
        const string systemSurfaces = "system/data/surfaces.ini";
        var systemSurfacesChecksum = _checksumProvider.GetChecksumsForOther(systemSurfaces);
        AddChecksum(dict, systemSurfaces, defaultValue: systemSurfacesChecksum?.MD5);

        var realTrackPath = $"content/tracks/{track}";
        var virtualTrackPath = realTrackPath;
        if (!Directory.Exists(realTrackPath))
        {
            realTrackPath = $"content/tracks/{_configuration.CSPTrackOptions.Track}";
        }
        
        var trackChecksums = _checksumProvider.GetChecksumsForTrack(_configuration.CSPTrackOptions.Track, trackConfig);
        var defaultChecksums = surfaceFix ? trackChecksums?.CSP : trackChecksums?.Vanilla;

        if (string.IsNullOrEmpty(trackConfig))
        {
            AddChecksumVirtualPath(dict, realTrackPath, virtualTrackPath, "data/surfaces.ini", surfaceFix, defaultChecksums);
            AddChecksumVirtualPath(dict, realTrackPath, virtualTrackPath, "models.ini", defaultSums: defaultChecksums);
        }
        else
        {
            AddChecksumVirtualPath(dict, realTrackPath, virtualTrackPath, $"{trackConfig}/data/surfaces.ini", surfaceFix, defaultChecksums);
            AddChecksumVirtualPath(dict, realTrackPath, virtualTrackPath, $"models_{trackConfig}.ini", surfaceFix, defaultChecksums);
        }
        
        ChecksumDirectory(dict, realTrackPath, virtualTrackPath, defaultChecksums);

        TrackChecksums = dict;
    }

    private void CalculateCarChecksums(IEnumerable<string> cars, bool allowAlternatives)
    {
        var carDataChecksums = new Dictionary<string, Dictionary<string, byte[]>>();
        var additionalChecksums = new Dictionary<string, byte[]>();

        foreach (string car in cars)
        {
            var defaultSums = _checksumProvider.GetChecksumsForCar(car);
            
            string carFolder = $"content/cars/{car}";
            var colliderFile = $"{carFolder}/collider.kn5";
            AddChecksum(additionalChecksums, colliderFile, defaultValue: defaultSums?.GetValueOrDefault(colliderFile)?.MD5);
            
            var checksums = new Dictionary<string, byte[]>();
            if (allowAlternatives && Directory.Exists(carFolder))
            {
                foreach (string file in Directory.EnumerateFiles(carFolder, "data*.acd"))
                {
                    AddChecksum(checksums, file);
                }
            }
            else if (allowAlternatives && defaultSums != null)
            {
                foreach (var file in defaultSums.Where(x => x.Key.Split('/').Last().StartsWith("data") || x.Key.EndsWith(".acd")))
                {
                    AddDefaultChecksum(checksums, file.Key, file.Value.MD5);
                }
            }
            else
            {
                var acdPath = $"{carFolder}/data.acd";
                AddChecksum(checksums, acdPath, defaultValue: defaultSums?.GetValueOrDefault(acdPath)?.MD5);
            }

            carDataChecksums.Add(car, checksums);
        }

        CarChecksums = carDataChecksums;
        AdditionalCarChecksums = additionalChecksums;
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

    private static void AddChecksumVirtualPath(Dictionary<string, byte[]> dict, string path, string virtualPath, string file, bool surfaceFix = false, ChecksumFileList? defaultSums = null)
    {
        var physicalFile = $"{path}/{file}";
        var defaultSum = defaultSums?.GetValueOrDefault(physicalFile);
        AddChecksum(dict, physicalFile, $"{virtualPath}/{file}", surfaceFix, defaultSum?.MD5);
    }

    private static void AddChecksum(Dictionary<string, byte[]> dict, string filePath, string? name = null, bool surfaceFix = false, string? defaultValue = null)
    {
        if (TryCreateChecksum(filePath, out byte[]? checksum, surfaceFix))
        {
            dict.Add(name ?? filePath, checksum);
            Log.Debug("Added checksum ({Checksum}) for {Path}", Convert.ToHexStringLower(checksum), name ?? filePath);
        }
        else if (defaultValue != null)
        {
            AddDefaultChecksum(dict, name ?? filePath, defaultValue);
        }
    }

    private static void AddDefaultChecksum(Dictionary<string, byte[]> dict, string virtualPath, string defaultValue)
    {
        var defaultChecksum = Convert.FromHexString(defaultValue);
        dict.Add(virtualPath, defaultChecksum);
        Log.Debug("Added checksum ({Checksum}) for {Path} from Default List", defaultValue, virtualPath);
    }
    
    private static void ChecksumDirectory(Dictionary<string, byte[]> dict, string path, string? virtualPath = null, ChecksumFileList? defaultSums = null)
    {
        virtualPath ??= path;
        
        if (!Directory.Exists(path))
        {
            DefaultChecksumDirectory(dict, path, defaultSums);
            return;
        }

        var files = Directory.GetFiles(path);
        if (files.Length == 0)
        {
            DefaultChecksumDirectory(dict, path, defaultSums);
            return;
        }
        
        foreach (var file in files)
        {
            var name = Path.GetFileName(file);
            var virtualName = $"{virtualPath}/{name.Replace("\\", "/")}";
            
            if (name == "surfaces.ini" || name.EndsWith(".kn5"))
            {
                AddChecksum(dict, file, virtualName);
            }
        }
    }

    private static void DefaultChecksumDirectory(Dictionary<string, byte[]> dict, string path, ChecksumFileList? defaultSums = null)
    {
        if (defaultSums == null)
            return;
        
        foreach (var file in defaultSums.Where(x => x.Key.Split('/').Last() == "surfaces.ini" || x.Key.EndsWith(".kn5")))
        {
            AddDefaultChecksum(dict, file.Key, file.Value.MD5);
        }
    }
}
