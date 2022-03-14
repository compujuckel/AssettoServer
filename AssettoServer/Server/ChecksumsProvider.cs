using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Security.Cryptography;
using Serilog;

namespace AssettoServer.Server;

public static class ChecksumsProvider
{
    public static ImmutableDictionary<string, byte[]> CalculateTrackChecksums(string track, string trackConfig)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, byte[]>();
        
        AddChecksum(builder, "system/data/surfaces.ini");

        string trackPath = $"content/tracks/{track}";

        if (string.IsNullOrEmpty(trackConfig))
        {
            AddChecksum(builder, $"{trackPath}/data/surfaces.ini");
            AddChecksum(builder, $"{trackPath}/models.ini");
        }
        else
        {
            AddChecksum(builder, $"{trackPath}/{trackConfig}/data/surfaces.ini");
            AddChecksum(builder, $"{trackPath}/models_{trackConfig}.ini");
        }
        
        ChecksumDirectory(builder, trackPath);

        return builder.ToImmutable();
    }

    public static ImmutableDictionary<string, byte[]> CalculateCarChecksums(IEnumerable<string> cars)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, byte[]>();
        
        foreach (string car in cars)
        {
            AddChecksum(builder, $"content/cars/{car}/data.acd", car);
        }
        
        return builder.ToImmutable();
    }

    private static bool TryCreateChecksum(string filePath, [MaybeNullWhen(false)] out byte[] checksum)
    {
        if (File.Exists(filePath))
        {
            using var md5 = MD5.Create();
            using var fileStream = File.OpenRead(filePath);
            checksum = md5.ComputeHash(fileStream);
            return true;
        }

        checksum = null;
        return false;
    }
    
    private static void AddChecksum(ImmutableDictionary<string, byte[]>.Builder builder, string filePath, string? name = null)
    {
        if (TryCreateChecksum(filePath, out byte[]? checksum))
        {
            builder.Add(name ?? filePath, checksum);
            Log.Debug("Added checksum for {Path}", name ?? filePath);
        }
    }
    
    private static void ChecksumDirectory(ImmutableDictionary<string, byte[]>.Builder builder, string directory)
    {
        string[] allFiles = Directory.GetFiles(directory);
        foreach (string file in allFiles)
        {
            string name = Path.GetFileName(file);

            if (name == "surfaces.ini" || name.EndsWith(".kn5"))
                AddChecksum(builder, file, file.Replace("\\", "/"));
        }
    }
}
