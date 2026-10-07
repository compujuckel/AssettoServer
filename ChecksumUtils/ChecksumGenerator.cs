using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using AssettoServer.Shared.Checksum;
using Serilog;

namespace ChecksumUtils;

public sealed record SkippedChecksum(
    string ContentPath,
    string? ExistingMd5,
    string? LocalMd5,
    string? ExistingSha256,
    string? LocalSha256);

public sealed class ChecksumUpdateSummary
{
    public List<SkippedChecksum> SkippedChecksums { get; } = [];
}

public static class ChecksumGenerator
{
    private static readonly HashSet<string> KunosCarsWithoutKsPrefix = new(StringComparer.OrdinalIgnoreCase)
    {
        // Add Kunos car folder names that do not start with "ks_" here.
        "abarth500",
        "abarth500_s1",
        "alfa_romeo_giulietta_qv",
        "alfa_romeo_giulietta_qv_le",
        "bmw_1m",
        "bmw_1m_s3",
        "bmw_m3_e30",
        "bmw_m3_e30_drift",
        "bmw_m3_e30_dtm",
        "bmw_m3_e30_gra",
        "bmw_m3_e30_s1",
        "bmw_m3_e92",
        "bmw_m3_e92_drift",
        "bmw_m3_e92_s1",
        "bmw_m3_gt2",
        "bmw_z4",
        "bmw_z4_drift",
        "bmw_z4_gt3",
        "bmw_z4_s1",
        "ferrari_312t",
        "ferrari_458",
        "ferrari_458_gt2",
        "ferrari_458_s3",
        "ferrari_599xxevo",
        "ferrari_f40",
        "ferrari_f40_s3",
        "ferrari_laferrari",
        "ktm_xbow_r",
        "lotus_2_eleven",
        "lotus_2_eleven_gt4",
        "lotus_49",
        "lotus_98t",
        "lotus_elise_sc",
        "lotus_elise_sc_s1",
        "lotus_elise_sc_s2",
        "lotus_evora_gtc",
        "lotus_evora_gte",
        "lotus_evora_gte_carbon",
        "lotus_evora_gx",
        "lotus_evora_s",
        "lotus_evora_s_s2",
        "lotus_exige_240",
        "lotus_exige_240_s3",
        "lotus_exige_s",
        "lotus_exige_scura",
        "lotus_exige_s_roadster",
        "lotus_exige_v6_cup",
        "lotus_exos_125",
        "lotus_exos_125_s1",
        "mclaren_mp412c",
        "mclaren_mp412c_gt3",
        "mercedes_sls",
        "mercedes_sls_gt3",
        "p4-5_2011",
        "pagani_huayra",
        "pagani_zonda_r",
        "ruf_yellowbird",
        "shelby_cobra_427sc",
        "tatuusfa1"
    };

    private static readonly HashSet<string> KunosTracksWithoutKsPrefix = new(StringComparer.OrdinalIgnoreCase)
    {
        "drift",
        "imola",
        "magione",
        "monza",
        "mugello",
        "spa",
        "trento-bondone"
    };

    public static ChecksumUpdateSummary UpdateFromLocalContent(
        ChecksumsFile kunos,
        ChecksumsFile custom,
        string assettoDirectory,
        bool replace = false)
    {
        if (!Directory.Exists(assettoDirectory))
            throw new DirectoryNotFoundException($"Assetto Corsa directory not found: {assettoDirectory}");

        string contentDirectory = Path.Combine(assettoDirectory, "content");
        var local = new ChecksumsFile();
        ScanCars(local, Path.Combine(contentDirectory, "cars"));
        ScanTracks(local, Path.Combine(contentDirectory, "tracks"), supportsLayouts: true);
        ScanTracks(local, Path.Combine(contentDirectory, "track"), supportsLayouts: false);
        ScanOther(local, assettoDirectory);

        var summary = new ChecksumUpdateSummary();
        foreach (var (name, car) in local.Cars)
        {
            var target = IsKunosCar(name, kunos.Cars) ? kunos.Cars : custom.Cars;
            MergeLocalCar(target, name, car, replace, summary);
        }

        foreach (var (name, track) in local.Tracks)
        {
            var target = IsKunosTrack(name, kunos.Tracks) ? kunos.Tracks : custom.Tracks;
            MergeLocalTrack(target, name, track, replace, summary);
        }

        foreach (var (path, checksum) in local.Other)
            SetByPath(kunos.Other, path, checksum);

        return summary;
    }

    private static bool IsKunosCar(string name, Dictionary<string, CarChecksum> kunosCars)
    {
        return name.StartsWith("ks_", StringComparison.OrdinalIgnoreCase)
               || KunosCarsWithoutKsPrefix.Contains(name)
               || FindKey(kunosCars, name) != null;
    }

    private static bool IsKunosTrack(string name, Dictionary<string, TrackChecksum> kunosTracks)
    {
        return name.StartsWith("ks_", StringComparison.OrdinalIgnoreCase)
               || KunosTracksWithoutKsPrefix.Contains(name)
               || FindKey(kunosTracks, name) != null;
    }

    private static void ScanCars(ChecksumsFile checksums, string carsDirectory)
    {
        if (!Directory.Exists(carsDirectory))
            return;

        foreach (string carDirectory in Directory.EnumerateDirectories(carsDirectory))
        {
            string carName = Path.GetFileName(carDirectory);
            string uiPath = Path.Combine(carDirectory, "ui", "ui_car.json");
            var local = new CarChecksum { Info = ReadInfo(uiPath, isCar: true) };

            string collider = Path.Combine(carDirectory, "collider.kn5");
            if (File.Exists(collider))
                local.Files["collider.kn5"] = CalculateChecksum(collider);

            foreach (string dataFile in Directory.EnumerateFiles(carDirectory, "data*.acd", SearchOption.TopDirectoryOnly))
                local.Files[Path.GetFileName(dataFile)] = CalculateChecksum(dataFile);

            checksums.Cars[carName] = local;
        }
    }

    private static void ScanTracks(ChecksumsFile checksums, string tracksDirectory, bool supportsLayouts)
    {
        if (!Directory.Exists(tracksDirectory))
            return;

        foreach (string trackDirectory in Directory.EnumerateDirectories(tracksDirectory))
        {
            string trackName = Path.GetFileName(trackDirectory);
            string[] layoutFiles = supportsLayouts
                ? Directory.GetFiles(trackDirectory, "models_*.ini", SearchOption.TopDirectoryOnly)
                : [];

            var local = layoutFiles.Length > 0
                ? BuildTrackWithLayouts(trackDirectory, layoutFiles)
                : BuildTrackWithoutLayouts(trackDirectory);

            if (local != null)
            {
                string? existingKey = FindKey(checksums.Tracks, trackName);
                if (existingKey == null)
                    checksums.Tracks[trackName] = local;
                else
                    MergeScannedTrack(checksums.Tracks[existingKey], local);
            }
        }
    }

    private static void MergeScannedTrack(TrackChecksum existing, TrackChecksum local)
    {
        MergeLocalEntry(existing, local);
        foreach (var (layoutName, localLayout) in local.Layouts)
        {
            string? existingLayoutKey = FindKey(existing.Layouts, layoutName);
            if (existingLayoutKey == null)
                existing.Layouts[layoutName] = localLayout;
            else
                MergeLocalEntry(existing.Layouts[existingLayoutKey], localLayout);
        }
    }

    private static TrackChecksum? BuildTrackWithLayouts(string trackDirectory, string[] layoutFiles)
    {
        var track = new TrackChecksum();
        AddTrackRootFiles(track, trackDirectory);

        foreach (string modelsFile in layoutFiles)
        {
            string fileName = Path.GetFileName(modelsFile);
            string layoutName = fileName["models_".Length..^".ini".Length];
            if (string.IsNullOrWhiteSpace(layoutName))
                continue;

            string uiPath = Path.Combine(trackDirectory, "ui", layoutName, "ui_track.json");
            var layout = new TrackLayoutChecksum { Info = ReadInfo(uiPath, isCar: false) };
            layout.Files[fileName] = CalculateChecksum(modelsFile);

            string surfacesPath = Path.Combine(trackDirectory, layoutName, "data", "surfaces.ini");
            if (File.Exists(surfacesPath))
                layout.Surfaces[$"{layoutName}/data/surfaces.ini"] = CalculateSurfaceChecksums(surfacesPath);

            track.Layouts[layoutName] = layout;
        }

        return track.Layouts.Count > 0 ? track : null;
    }

    private static TrackChecksum? BuildTrackWithoutLayouts(string trackDirectory)
    {
        string modelsPath = Path.Combine(trackDirectory, "models.ini");
        if (!File.Exists(modelsPath))
            return null;

        string uiPath = Path.Combine(trackDirectory, "ui", "ui_track.json");
        var track = new TrackChecksum { Info = ReadInfo(uiPath, isCar: false) };
        track.Files["models.ini"] = CalculateChecksum(modelsPath);
        AddTrackRootFiles(track, trackDirectory);

        string dataSurfacesPath = Path.Combine(trackDirectory, "data", "surfaces.ini");
        if (File.Exists(dataSurfacesPath))
            track.Surfaces["data/surfaces.ini"] = CalculateSurfaceChecksums(dataSurfacesPath);

        return track;
    }

    private static void AddTrackRootFiles(TrackChecksum track, string trackDirectory)
    {
        foreach (string file in Directory.EnumerateFiles(trackDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            string fileName = Path.GetFileName(file);
            if (fileName.EndsWith(".kn5", StringComparison.OrdinalIgnoreCase))
            {
                track.Files[fileName] = CalculateChecksum(file);
            }

            if (fileName.Equals("surfaces.ini", StringComparison.OrdinalIgnoreCase))
                track.Surfaces[fileName] = CalculateSurfaceChecksums(file);
        }
    }

    private static void ScanOther(ChecksumsFile checksums, string assettoDirectory)
    {
        string surfacesPath = Path.Combine(assettoDirectory, "system", "data", "surfaces.ini");
        if (File.Exists(surfacesPath))
            checksums.Other["system/data/surfaces.ini"] = CalculateChecksum(surfacesPath);
    }

    private static void MergeLocalCar(
        Dictionary<string, CarChecksum> cars,
        string name,
        CarChecksum local,
        bool replace,
        ChecksumUpdateSummary summary)
    {
        string? existingKey = FindKey(cars, name);
        if (existingKey == null)
        {
            cars[name] = local;
            return;
        }

        var existing = cars[existingKey];
        if (!ShouldUpdate(existing.Info?.Version, local.Info?.Version, replace))
        {
            AddMismatchedChecksums(existing, local, $"content/cars/{name}", summary);
            return;
        }

        MergeLocalEntry(existing, local);
    }

    private static void MergeLocalTrack(
        Dictionary<string, TrackChecksum> tracks,
        string name,
        TrackChecksum local,
        bool replace,
        ChecksumUpdateSummary summary)
    {
        string? existingKey = FindKey(tracks, name);
        if (existingKey == null)
        {
            tracks[name] = local;
            return;
        }

        var existing = tracks[existingKey];
        if (ShouldUpdateTrackCommonFiles(existing, local, replace))
            MergeLocalEntry(existing, local);
        else
            AddMismatchedChecksums(existing, local, $"content/tracks/{name}", summary);

        foreach (var (layoutName, localLayout) in local.Layouts)
        {
            string? existingLayoutKey = FindKey(existing.Layouts, layoutName);
            if (existingLayoutKey == null)
            {
                existing.Layouts[layoutName] = localLayout;
                continue;
            }

            var existingLayout = existing.Layouts[existingLayoutKey];
            if (ShouldUpdate(existingLayout.Info?.Version, localLayout.Info?.Version, replace))
                MergeLocalEntry(existingLayout, localLayout);
            else
                AddMismatchedChecksums(
                    existingLayout, localLayout, $"content/tracks/{name}", summary);
        }
    }

    private static bool ShouldUpdateTrackCommonFiles(TrackChecksum existing, TrackChecksum local, bool replace)
    {
        if (replace)
            return true;

        if (local.Layouts.Count == 0)
            return ShouldUpdate(existing.Info?.Version, local.Info?.Version, replace);

        foreach (var (existingLayoutName, existingLayout) in existing.Layouts)
        {
            string? localLayoutKey = FindKey(local.Layouts, existingLayoutName);
            if (localLayoutKey == null
                || !ShouldUpdate(
                    existingLayout.Info?.Version, local.Layouts[localLayoutKey].Info?.Version, replace))
                return false;
        }

        return true;
    }

    private static void AddMismatchedChecksums(
        ChecksumEntry existing,
        ChecksumEntry local,
        string contentPath,
        ChecksumUpdateSummary summary)
    {
        foreach (var (path, localChecksum) in local.Files)
        {
            string? existingKey = FindKey(existing.Files, path);
            if (existingKey != null && HasChecksumMismatch(existing.Files[existingKey], localChecksum))
                AddSkippedChecksum($"{contentPath}/{NormalizePath(path)}", existing.Files[existingKey], localChecksum, summary);
        }

        if (existing is not TrackChecksumEntry existingTrack || local is not TrackChecksumEntry localTrack)
            return;

        foreach (var (path, localVariants) in localTrack.Surfaces)
        {
            string? existingKey = FindKey(existingTrack.Surfaces, path);
            if (existingKey == null)
                continue;

            var existingVariants = existingTrack.Surfaces[existingKey];
            AddMismatchedVariant(
                $"{contentPath}/{NormalizePath(path)} [vanilla]", existingVariants.Vanilla, localVariants.Vanilla, summary);
            AddMismatchedVariant(
                $"{contentPath}/{NormalizePath(path)} [CSP]", existingVariants.Csp, localVariants.Csp, summary);
        }
    }

    private static void AddMismatchedVariant(
        string contentPath,
        ChecksumValue? existing,
        ChecksumValue? local,
        ChecksumUpdateSummary summary)
    {
        if (existing != null && local != null && HasChecksumMismatch(existing, local))
            AddSkippedChecksum(contentPath, existing, local, summary);
    }

    private static void AddSkippedChecksum(
        string contentPath,
        ChecksumValue existing,
        ChecksumValue local,
        ChecksumUpdateSummary summary)
    {
        summary.SkippedChecksums.Add(new SkippedChecksum(
            contentPath,
            existing.MD5?.ToHexString(),
            local.MD5?.ToHexString(),
            existing.SHA256?.ToHexString(),
            local.SHA256?.ToHexString()));
    }

    private static bool HasChecksumMismatch(ChecksumValue existing, ChecksumValue local)
    {
        return DiffersWhenBothPresent(existing.MD5, local.MD5)
               || DiffersWhenBothPresent(existing.SHA256, local.SHA256);
    }

    private static bool DiffersWhenBothPresent(Md5Checksum? existing, Md5Checksum? local)
    {
        return existing.HasValue && local.HasValue && existing.Value != local.Value;
    }

    private static bool DiffersWhenBothPresent(Sha256Checksum? existing, Sha256Checksum? local)
    {
        return existing.HasValue && local.HasValue && existing.Value != local.Value;
    }

    private static void MergeLocalEntry(ChecksumEntry existing, ChecksumEntry local)
    {
        if (local.Info != null)
        {
            existing.Info ??= new ChecksumInfo();
            MergeInfoFromLocal(existing.Info, local.Info);
        }
        else if (existing.Info != null)
        {
            existing.Info.Version = null;
        }

        foreach (var (path, checksum) in local.Files)
            SetByPath(existing.Files, path, checksum);

        if (existing is not TrackChecksumEntry existingTrack || local is not TrackChecksumEntry localTrack)
            return;

        foreach (var (path, variants) in localTrack.Surfaces)
        {
            string? existingKey = FindKey(existingTrack.Surfaces, path);
            if (existingKey == null)
            {
                existingTrack.Surfaces[path] = variants;
                continue;
            }

            var existingVariants = existingTrack.Surfaces[existingKey];
            if (variants.Vanilla != null)
                existingVariants.Vanilla = variants.Vanilla;
            if (variants.Csp != null)
                existingVariants.Csp = variants.Csp;
        }
    }

    private static void MergeInfoFromLocal(ChecksumInfo existing, ChecksumInfo local)
    {
        existing.Name = local.Name ?? existing.Name;
        existing.Country = local.Country ?? existing.Country;
        existing.Version = local.Version;
        existing.Author = local.Author ?? existing.Author;
        existing.Year = local.Year ?? existing.Year;
        existing.Pitboxes = local.Pitboxes ?? existing.Pitboxes;
        existing.Brand = local.Brand ?? existing.Brand;
        existing.Bhp = local.Bhp ?? existing.Bhp;
        existing.Torque = local.Torque ?? existing.Torque;
        existing.Weight = local.Weight ?? existing.Weight;
    }

    private static bool ShouldUpdate(string? storedVersion, string? localVersion, bool replace)
    {
        if (replace)
            return true;

        if (!TryParseVersion(storedVersion, out var stored) || !TryParseVersion(localVersion, out var local))
            return true;

        return local >= stored;
    }

    private static bool TryParseVersion(string? value, out Version version)
    {
        string normalized = value?.Trim() ?? "";
        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
            normalized = normalized[1..];

        if (Version.TryParse(normalized, out var parsed))
        {
            version = parsed;
            return true;
        }

        version = null!;
        return false;
    }

    private static ChecksumInfo? ReadInfo(string path, bool isCar)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            using var stream = File.OpenRead(path);
            var json = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(stream);
            if (json == null)
                return null;

            string? Value(string key)
            {
                foreach (var (name, value) in json)
                {
                    if (string.Equals(name, key, StringComparison.OrdinalIgnoreCase))
                        return value.ToString();
                }

                return null;
            }

            return new ChecksumInfo
            {
                Name = Value("name"),
                Country = Value("country"),
                Version = Value("version"),
                Author = Value("author"),
                Year = Value("year"),
                Pitboxes = isCar ? null : Value("pitboxes"),
                Brand = isCar ? Value("brand") : null,
                Bhp = isCar ? Value("bhp") : null,
                Torque = isCar ? Value("torque") : null,
                Weight = isCar ? Value("weight") : null
            };
        }
        catch (JsonException ex)
        {
            Log.Warning(ex, "Could not read optional UI metadata from {Path}", path);
            return null;
        }
        catch (IOException ex)
        {
            Log.Warning(ex, "Could not read optional UI metadata from {Path}", path);
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            Log.Warning(ex, "Could not read optional UI metadata from {Path}", path);
            return null;
        }
    }

    private static SurfaceChecksumVariants CalculateSurfaceChecksums(string filePath)
    {
        byte[] bytes = File.ReadAllBytes(filePath);
        var vanilla = CalculateChecksum(bytes);
        int firstSurface = bytes.AsSpan().IndexOf("SURFACE_0"u8);
        if (firstSurface > 0)
            "CSP"u8.CopyTo(bytes.AsSpan(firstSurface, 3));

        return new SurfaceChecksumVariants
        {
            Vanilla = vanilla,
            Csp = firstSurface > 0 ? CalculateChecksum(bytes) : vanilla
        };
    }

    private static ChecksumValue CalculateChecksum(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> buffer = stackalloc byte[8192];
        int bytesRead;
        while ((bytesRead = stream.Read(buffer)) > 0)
        {
            md5.AppendData(buffer[..bytesRead]);
            sha256.AppendData(buffer[..bytesRead]);
        }

        Md5Checksum md5Checksum = default;
        Sha256Checksum sha256Checksum = default;
        md5.GetHashAndReset(md5Checksum);
        sha256.GetHashAndReset(sha256Checksum);
        return new ChecksumValue
        {
            MD5 = md5Checksum,
            SHA256 = sha256Checksum
        };
    }

    private static ChecksumValue CalculateChecksum(ReadOnlySpan<byte> bytes)
    {
        Md5Checksum md5 = default;
        Sha256Checksum sha256 = default;
        MD5.HashData(bytes, md5);
        SHA256.HashData(bytes, sha256);

        return new ChecksumValue
        {
            MD5 = md5,
            SHA256 = sha256
        };
    }

    private static void SetByPath(Dictionary<string, ChecksumValue> values, string path, ChecksumValue value)
    {
        string? existingKey = FindKey(values, path);
        values[existingKey ?? path] = value;
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/').TrimStart('/');
    }

    private static string? FindKey<T>(Dictionary<string, T> values, string key)
    {
        if (values.ContainsKey(key))
            return key;

        return values.Keys.FirstOrDefault(existing => string.Equals(existing, key, StringComparison.OrdinalIgnoreCase));
    }
}
