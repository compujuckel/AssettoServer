using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AssettoServer.Shared.Checksum;

public sealed class ChecksumsFile
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public Dictionary<string, TrackChecksum> Tracks { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, CarChecksum> Cars { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, ChecksumValue> Other { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public static ChecksumsFile FromJson(string json)
    {
        return JsonSerializer.Deserialize<ChecksumsFile>(json, SerializerOptions)!;
    }

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, SerializerOptions);
    }

    public ChecksumsFile SelectActive(string trackName, string? trackConfig, IEnumerable<string> cars)
    {
        var active = new ChecksumsFile();
        var track = Find(Tracks, trackName);
        if (track != null)
        {
            var selectedTrack = new TrackChecksum
            {
                Info = track.Info,
                Files = track.Files,
                Surfaces = track.Surfaces
            };
            if (!string.IsNullOrEmpty(trackConfig) && Find(track.Layouts, trackConfig) is { } layout)
                selectedTrack.Layouts.Add(trackConfig, layout);
            active.Tracks.Add(trackName, selectedTrack);
        }

        foreach (string car in cars)
        {
            if (Find(Cars, car) is { } entry)
                active.Cars.TryAdd(car, entry);
        }

        if (Find(Other, "system/data/surfaces.ini") is { } systemSurfaces)
            active.Other.Add("system/data/surfaces.ini", systemSurfaces);

        return active;
    }

    public void MergeMissingFrom(ChecksumsFile lowerPriority)
    {
        MergeEntries(Tracks, lowerPriority.Tracks, MergeTrack);
        MergeEntries(Cars, lowerPriority.Cars, MergeEntry);
        MergeChecksums(Other, lowerPriority.Other);
    }

    public bool TryGetTrack(string trackName, string? trackConfig, [NotNullWhen(true)] out TrackChecksumEntry? trackChecksums)
    {
        var track = Find(Tracks, trackName);
        trackChecksums = null;
        if (track == null)
            return false;

        trackChecksums = string.IsNullOrEmpty(trackConfig) ? track : Find(track.Layouts, trackConfig);
        return trackChecksums != null;
    }

    public static T? Find<T>(Dictionary<string, T> items, string key)
    {
        if (items.TryGetValue(key, out var exactMatch))
            return exactMatch;

        if (items.Comparer.Equals(StringComparer.OrdinalIgnoreCase))
            return default;

        foreach (var (existingKey, value) in items)
        {
            if (string.Equals(existingKey, key, StringComparison.OrdinalIgnoreCase))
                return value;
        }

        return default;
    }

    private static void MergeTrack(TrackChecksum target, TrackChecksum lowerPriority)
    {
        MergeEntry(target, lowerPriority);
        MergeEntries(target.Layouts, lowerPriority.Layouts, MergeEntry);
    }

    private static void MergeEntry(ChecksumEntry target, ChecksumEntry lowerPriority)
    {
        if (target.Info == null)
        {
            target.Info = lowerPriority.Info;
        }
        else if (lowerPriority.Info != null)
        {
            target.Info.MergeMissingFrom(lowerPriority.Info);
        }

        MergeChecksums(target.Files, lowerPriority.Files);
        if (target is TrackChecksumEntry targetTrack && lowerPriority is TrackChecksumEntry lowerPriorityTrack)
            MergeSurfaces(targetTrack.Surfaces, lowerPriorityTrack.Surfaces);
    }

    private static void MergeEntries<T>(
        Dictionary<string, T> target,
        Dictionary<string, T> lowerPriority,
        Action<T, T> merge)
        where T : class
    {
        foreach (var (key, lowerValue) in lowerPriority)
        {
            var existing = Find(target, key);
            if (existing == null)
            {
                target.Add(key, lowerValue);
            }
            else
            {
                merge(existing, lowerValue);
            }
        }
    }

    private static void MergeChecksums(
        Dictionary<string, ChecksumValue> target,
        Dictionary<string, ChecksumValue> lowerPriority)
    {
        MergeEntries(target, lowerPriority, (higher, lower) => higher.MergeMissingFrom(lower));
    }

    private static void MergeSurfaces(
        Dictionary<string, SurfaceChecksumVariants> target,
        Dictionary<string, SurfaceChecksumVariants> lowerPriority)
    {
        MergeEntries(target, lowerPriority, (higher, lower) =>
        {
            if (higher.Vanilla == null)
                higher.Vanilla = lower.Vanilla;
            else if (lower.Vanilla != null)
                higher.Vanilla.MergeMissingFrom(lower.Vanilla);

            if (higher.Csp == null)
                higher.Csp = lower.Csp;
            else if (lower.Csp != null)
                higher.Csp.MergeMissingFrom(lower.Csp);
        });
    }

}

public class ChecksumEntry
{
    public ChecksumInfo? Info { get; set; }
    public Dictionary<string, ChecksumValue> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public abstract class TrackChecksumEntry : ChecksumEntry
{
    public Dictionary<string, SurfaceChecksumVariants> Surfaces { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class TrackChecksum : TrackChecksumEntry
{
    public Dictionary<string, TrackLayoutChecksum> Layouts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class TrackLayoutChecksum : TrackChecksumEntry
{
}

public sealed class CarChecksum : ChecksumEntry
{
}

public sealed class SurfaceChecksumVariants
{
    public ChecksumValue? Vanilla { get; set; }
    public ChecksumValue? Csp { get; set; }
}

public sealed class ChecksumInfo
{
    public string? Name { get; set; }
    public string? Country { get; set; }
    public string? Version { get; set; }
    public string? Author { get; set; }
    public string? Year { get; set; }
    public string? Pitboxes { get; set; }
    public string? Brand { get; set; }
    public string? Bhp { get; set; }
    public string? Torque { get; set; }
    public string? Weight { get; set; }

    public void MergeMissingFrom(ChecksumInfo lowerPriority)
    {
        Name ??= lowerPriority.Name;
        Country ??= lowerPriority.Country;
        Version ??= lowerPriority.Version;
        Author ??= lowerPriority.Author;
        Year ??= lowerPriority.Year;
        Pitboxes ??= lowerPriority.Pitboxes;
        Brand ??= lowerPriority.Brand;
        Bhp ??= lowerPriority.Bhp;
        Torque ??= lowerPriority.Torque;
        Weight ??= lowerPriority.Weight;
    }
}

public sealed class ChecksumValue
{
    public Md5Checksum? MD5 { get; set; }
    public Sha256Checksum? SHA256 { get; set; }

    public void MergeMissingFrom(ChecksumValue lowerPriority)
    {
        MD5 ??= lowerPriority.MD5;
        SHA256 ??= lowerPriority.SHA256;
    }
}
