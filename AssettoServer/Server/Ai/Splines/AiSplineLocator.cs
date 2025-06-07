using System;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using AssettoServer.Server.Configuration;
using Serilog;
using SerilogTimings;

namespace AssettoServer.Server.Ai.Splines;

public class AiSplineLocator
{
    private readonly ACServerConfiguration _configuration;
    private readonly FastLaneParser _parser;
    private readonly AiSplineWriter _writer;

    public AiSplineLocator(ACServerConfiguration configuration, FastLaneParser parser, AiSplineWriter writer)
    {
        _configuration = configuration;
        _parser = parser;
        _writer = writer;
    }

    public AiSpline Locate()
    {
        try
        {
            return LocateInternal(_configuration.Server.Track, _configuration.Server.TrackConfig);
        }
        catch (Exception)
        {
            if (_configuration.CSPTrackOptions.MinimumCSPVersion.HasValue)
            {
                return LocateInternal(_configuration.CSPTrackOptions.Track, _configuration.Server.TrackConfig);
            }

            throw;
        }
    }

    private AiSpline LocateInternal(string track, string trackConfig)
    {
        string contentPath = "content";
        const string contentPathCMWorkaround = "content~tmp";
        // CM renames the content folder to content~tmp when enabling the "Disable integrity verification" checkbox. We still need to load an AI spline from there, even when checksums are disabled
        if (!Directory.Exists(contentPath) && Directory.Exists(contentPathCMWorkaround))
        {
            contentPath = contentPathCMWorkaround;
        }

        string mapAiBasePath = Path.Join(contentPath, $"tracks/{track}/ai/");
        if (!string.IsNullOrEmpty(trackConfig)
            && Path.Join(contentPath, $"tracks/{track}/{trackConfig}/ai/") is var mapAiLayoutPath
            && Directory.Exists(mapAiLayoutPath))
        {
            mapAiBasePath = mapAiLayoutPath;
        }
        
        var cacheKey = GenerateCacheKey(mapAiBasePath);
        Directory.CreateDirectory("cache");
        var cachePath = Path.Join("cache", $"{cacheKey}.aic{AiSpline.SupportedVersion}");
        if (!File.Exists(cachePath))
        {
            Log.Information("Cached AI spline not found. Generating cache...");
            using var t = Operation.Time("Generating cache");
            var aiPackage = _parser.FromFiles(mapAiBasePath);
            _writer.ToFile(aiPackage, cachePath);
        }

        return new AiSpline(cachePath);
    }

    private static string GenerateCacheKey(string folder)
    {
        var hash = new XxHash64();
        
        string aipPath = Path.Join(folder, "fast_lane.aip");
        if (File.Exists(aipPath))
        {
            using var stream = File.OpenRead(aipPath);
            hash.Append(stream);
            return Convert.ToHexString(hash.GetCurrentHash());
        }

        string configPath = Path.Join(folder, "config.yml");
        if (File.Exists(configPath))
        {
            using var stream = File.OpenRead(configPath);
            hash.Append(stream);
        }
            
        if (!Directory.Exists(folder))
        {
            throw new ConfigurationException($"No ai folder found. Please put at least one AI spline fast_lane.ai(p) into {Path.GetFullPath(folder)}");
        }
            
        foreach (string file in Directory.EnumerateFiles(folder, "fast_lane*.ai").OrderBy(f => f))
        {
            using var stream = File.OpenRead(file);
            hash.Append(stream);
        }
            
        return Convert.ToHexString(hash.GetCurrentHash());
    }
}
