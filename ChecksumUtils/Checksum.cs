using System.Security.Cryptography;
using AssettoServer.Server.Checksum;
using Serilog;
using YamlDotNet.Serialization;

namespace ChecksumUtils;

public static class Checksum
{
    private const string SystemDataSurfacesKey = "system/data/surfaces.ini";
    private const string ContentCarsKey = "content/cars";
    private const string ContentTracksKey = "content/tracks";
    private static readonly string SystemDataSurfacesPath = Path.Join("system", "data", "surfaces.ini");
    private static readonly string ContentCarsPath = Path.Join("content", "cars");
    private static readonly string ContentTracksPath = Path.Join("content", "tracks");
    
    public static void AddNewSums(this ChecksumsFile checksums, string dir, bool replace)
    {
        if (!Directory.Exists(dir))
            throw new DirectoryNotFoundException("AC directory not found");

        var cars = Path.Join(dir, ContentCarsPath);
        checksums.CalculateCarSums(cars, replace);
        
        var tracks = Path.Join(dir, ContentTracksPath);
        checksums.CalculateTrackSums(tracks, replace);
        
        AddOrUpdateChecksum(checksums.Other, SystemDataSurfacesKey, Path.Join(dir, SystemDataSurfacesPath), replace);
    }

    private static void CalculateCarSums(this ChecksumsFile checksums, string path, bool replace)
    {
        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException("Car content directory not found");
        
        foreach (string carFolder in Directory.EnumerateDirectories(path))
        {
            var carName = Path.GetFileName(carFolder);
            if (!checksums.Cars.ContainsKey(carName))
            {
                checksums.Cars.Add(carName, new ChecksumFileList());
            }
            
            var colliderFile = Path.Join(carFolder, "collider.kn5");
            if (File.Exists(colliderFile))
            {
                var colliderKey = $"{ContentCarsKey}/{carName}/collider.kn5";
                AddOrUpdateChecksum(checksums.Cars[carName], colliderKey, colliderFile, replace);
            }
            
            foreach (string file in Directory.EnumerateFiles(carFolder, "data*.acd"))
            {
                var fileKey = $"{ContentCarsKey}/{carName}/{Path.GetFileName(file)}";
                AddOrUpdateChecksum(checksums.Cars[carName], fileKey, file, replace);
            }
            
            Log.Debug("Finished checksums for car {Car}", carName);
        }
    }

    private static void CalculateTrackSums(this ChecksumsFile checksums, string path, bool replace)
    {
        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException("Track content directory not found");
        
        foreach (string trackFolder in Directory.EnumerateDirectories(path))
        {
            var trackName = Path.GetFileName(trackFolder);
            if (!checksums.Tracks.ContainsKey(trackName))
            {
                checksums.Tracks.Add(trackName, new TrackChecksum());
            }
            
            foreach (string modelsFile in Directory.EnumerateFiles(trackFolder, "models*.ini"))
            {
                var modelsFileName = Path.GetFileName(modelsFile);
                var isTrackLayout = modelsFileName != "models.ini";
                var trackLayout = isTrackLayout ? modelsFileName[(modelsFileName.IndexOf('_') + 1)..modelsFileName.IndexOf('.')] : "";

                TrackLayoutChecksum layoutChecksum;
                if (isTrackLayout)
                {
                    if (!checksums.Tracks[trackName].Layouts.ContainsKey(trackLayout))
                    {
                        checksums.Tracks[trackName].Layouts.Add(trackLayout, new TrackLayoutChecksum());
                    }
                    layoutChecksum = checksums.Tracks[trackName].Layouts[trackLayout];
                    
                }
                else
                {
                    layoutChecksum = checksums.Tracks[trackName].Default;
                }

                var modelsKey = $"{ContentTracksKey}/{trackName}/{modelsFileName}";
                // Add CSP Checksums
                AddOrUpdateChecksum(layoutChecksum.CSP, modelsKey, modelsFile, replace, isTrackLayout);
                // Add vanilla Checksums
                AddOrUpdateChecksum(layoutChecksum.Vanilla, modelsKey, modelsFile, replace);

                var surfacesFile = isTrackLayout
                    ? Path.Join(trackFolder, trackLayout, "data", "surfaces.ini")
                    : Path.Join(trackFolder, "data", "surfaces.ini");
                if (File.Exists(surfacesFile))
                {
                    var surfacesKey = $"{ContentTracksKey}/{trackName}/{(isTrackLayout ? $"{trackLayout}/" : "")}data/surfaces.ini";
                    // Add CSP Checksums
                    AddOrUpdateChecksum(layoutChecksum.CSP, surfacesKey, surfacesFile, replace, true);
                    // Add vanilla Checksums
                    AddOrUpdateChecksum(layoutChecksum.Vanilla, surfacesKey, surfacesFile, replace);
                }

                foreach (string directoryFile in Directory.GetFiles(trackFolder))
                {
                    var directoryFileName = Path.GetFileName(directoryFile);
                    var directoryFileKey = $"{ContentTracksKey}/{trackName}/{directoryFileName}";
            
                    if (directoryFileName == "surfaces.ini" || directoryFileName.EndsWith(".kn5"))
                    {
                        // Add CSP Checksums
                        AddOrUpdateChecksum(layoutChecksum.CSP, directoryFileKey, directoryFile, replace);
                        // Add vanilla Checksums
                        AddOrUpdateChecksum(layoutChecksum.Vanilla, directoryFileKey, directoryFile, replace);
                    }
                }
                
                if (isTrackLayout)
                {
                    checksums.Tracks[trackName].Layouts[trackLayout] = layoutChecksum;
                }
                else
                {
                    checksums.Tracks[trackName].Default = layoutChecksum;
                }
            }

            var track = checksums.Tracks[trackName];
            if ((track.Layouts.Count == 0 && track.Default.CSP.Count == 0 && track.Default.Vanilla.Count == 0) ||
                (track.Default.CSP.Count == 1 && track.Default.CSP.First().Key.EndsWith("data/surfaces.ini") &&
                 track.Default.Vanilla.Count == 1 && track.Default.Vanilla.First().Key.EndsWith("data/surfaces.ini")))
            {
                var surfacesFile = Path.Join(trackFolder, "data", "surfaces.ini");
                if (File.Exists(surfacesFile))
                {
                    var surfacesKey = $"{ContentTracksKey}/{trackName}/data/surfaces.ini";
                    // Add CSP Checksums
                    AddOrUpdateChecksum(checksums.Tracks[trackName].Default.CSP, surfacesKey, surfacesFile, replace,
                        true);
                    // Add vanilla Checksums
                    AddOrUpdateChecksum(checksums.Tracks[trackName].Default.Vanilla, surfacesKey, surfacesFile,
                        replace);
                }
            }

            Log.Debug("Finished checksums for track {Track}", trackName);
        }
    }

    private static ChecksumItem CalculateSumsForFile(string filePath, bool surfaceFix = false)
    {
        if (surfaceFix)
        {
            var bytes = File.ReadAllBytes(filePath);
            var firstSurface = MemoryExtensions.IndexOf(bytes, "SURFACE_0"u8);
            if (firstSurface > 0)
            {
                "CSP"u8.CopyTo(bytes.AsSpan(firstSurface, 3));
            }

            return new ChecksumItem
            {
                MD5 = Convert.ToHexStringLower(MD5.HashData(bytes)),
                SHA256 = Convert.ToHexStringLower(SHA256.HashData(bytes))
            };
        }
        else
        {
            var bytes = File.ReadAllBytes(filePath);
            return new ChecksumItem
            {
                MD5 = Convert.ToHexStringLower(MD5.HashData(bytes)),
                SHA256 = Convert.ToHexStringLower(SHA256.HashData(bytes))
            };
        }
    }
    
    private static void AddOrUpdateChecksum(Dictionary<string, ChecksumItem> dict, string fileKey, string filePath, bool replace, bool surfaceFix = false)
    {
        if (!File.Exists(filePath)) return;
        
        var fileFound = dict.ContainsKey(fileKey);
        if (!fileFound || (fileFound && replace))
        {
            var checksum = CalculateSumsForFile(filePath, surfaceFix);
            dict[fileKey] = checksum;
        }
    }
    
    public static void ToFile(this ChecksumsFile checksums, StreamWriter file)
    {
        var builder = new SerializerBuilder();
        builder.Build().Serialize(file, checksums);
    }
}
