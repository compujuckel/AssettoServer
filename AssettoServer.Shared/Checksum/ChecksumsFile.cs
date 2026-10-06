using System;
using System.Collections.Generic;
using System.Linq;
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

    public void MergeMissingFrom(ChecksumsFile lowerPriority)
    {
        MergeEntries(Tracks, lowerPriority.Tracks, MergeTrack);
        MergeEntries(Cars, lowerPriority.Cars, MergeEntry);
        MergeChecksums(Other, lowerPriority.Other);
    }

    public bool TryGetTrack(string trackName, string? layout, out TrackChecksum? track, out TrackLayoutChecksum? trackLayout)
    {
        track = Find(Tracks, trackName);
        trackLayout = null;
        if (track == null)
            return false;

        if (track.Layouts.Count > 0)
        {
            if (string.IsNullOrWhiteSpace(layout))
            {
                track = null;
                return false;
            }

            trackLayout = Find(track.Layouts, layout);
            if (trackLayout == null)
            {
                track = null;
                return false;
            }

            return true;
        }

        if (!string.IsNullOrWhiteSpace(layout))
        {
            track = null;
            return false;
        }

        return true;
    }

    public static T? Find<T>(Dictionary<string, T> items, string key)
    {
        if (items.TryGetValue(key, out var exactMatch))
            return exactMatch;

        return items.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase)).Value;
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
            string? existingKey = FindKey(target, key);
            if (existingKey == null)
            {
                target.Add(key, lowerValue);
            }
            else
            {
                merge(target[existingKey], lowerValue);
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

    private static string? FindKey<T>(Dictionary<string, T> items, string key)
    {
        return items.ContainsKey(key)
            ? key
            : items.Keys.FirstOrDefault(existing => string.Equals(existing, key, StringComparison.OrdinalIgnoreCase));
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
