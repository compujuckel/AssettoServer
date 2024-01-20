using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using AssettoServer.Server.Configuration;
using Serilog;

namespace AssettoServer.Server;

public class ChecksumManager
{
    public IReadOnlyDictionary<string, byte[]> TrackChecksums { get; private set; } = null!;
    public IReadOnlyDictionary<string, Dictionary<string, byte[]>> CarChecksums { get; private set; } = null!;
    public IReadOnlyDictionary<string, byte[]> AdditionalCarChecksums { get; private set; } = null!;

    private readonly ACServerConfiguration _configuration;
    private readonly EntryCarManager _entryCarManager;
    
    public ChecksumManager(ACServerConfiguration configuration, EntryCarManager entryCarManager)
    {
        _configuration = configuration;
        _entryCarManager = entryCarManager;
    }
    
    public void Initialize()
    {
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
