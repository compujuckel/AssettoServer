using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using AssettoServer.Server.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace AssettoServer.Server.CMContentProviders;

public class ContentManagerInitializer : IHostedService
{
    private readonly ACServerConfiguration _configuration;

    public ContentManagerInitializer(ACServerConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Create zip files when the application starts
        return CreateAllZipFilesAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task CreateAllZipFilesAsync()
    {
        var allTasks = new List<Task>();

        if (_configuration.WrapperParams?.CarDirectDownload == true)
        {
            // Create zip files for all cars
            string carsPath = Path.Combine("content", "cars");
            var carIds = Directory.GetDirectories(carsPath).Select(Path.GetFileName).Where(id => id != null);

            allTasks.AddRange(carIds.Select(carId => CreateCarZipAsync(carId!)));
        }

        if (_configuration.WrapperParams?.TrackDirectDownload == true)
        {
            allTasks.Add(CreateTrackZipAsync());
        }
        
        if (allTasks.Count > 0)
        {
            await Task.WhenAll(allTasks);
        }
    }

    private async Task CreateCarZipAsync(string carId)
    {
        Log.Information($"Creating zip file for car {carId}");
        
        string carPath = Path.Combine("content", "cars", carId);
        string zipPath = Path.Combine("content", "cars", $"{carId}.zip");
        if (!Directory.Exists(carPath)) return;

        try
        {
            if (!File.Exists(zipPath))
            {
                await Task.Run(() =>
                {
                    using var zipArchive = System.IO.Compression.ZipFile.Open(zipPath, System.IO.Compression.ZipArchiveMode.Create);
                    string[] files = Directory.GetFiles(carPath, "*", SearchOption.AllDirectories);
                    foreach (string file in files)
                    {
                        string relativePath = file.Substring(carPath.Length + 1);
                        var entry = zipArchive.CreateEntry(Path.Combine(carId, relativePath));
                        if (entry != null)
                        {
                            using var entryStream = entry.Open();
                            using var fileStream = File.OpenRead(file);
                            fileStream.CopyTo(entryStream);
                        }
                    }
                });
                Log.Information($"Zip file creation completed for car {carId}");
            }
            else
            {
                Log.Information($"Zip file already exists for car {carId}");
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Error creating zip file for car {carId}: {ex.Message}");
        }
    }

    private async Task CreateTrackZipAsync()
    {
        string track = _configuration.Server.Track.Substring(_configuration.Server.Track.LastIndexOf('/') + 1);

        Log.Information($"Creating zip file for track {track}");
        string regularTrackPath = Path.Combine("content", "tracks", track);
        string cspTrackPath = Path.Combine("content", "tracks", "csp", track);
        string zipPath;
        string trackPath;

        if (Directory.Exists(regularTrackPath))
        {
            trackPath = regularTrackPath;
            zipPath = Path.Combine("content", "tracks", $"{track}.zip");
        }
        else if (Directory.Exists(cspTrackPath))
        {
            trackPath = cspTrackPath;
            zipPath = Path.Combine("content", "tracks", "csp", $"{track}.zip");
        }
        else
        {
            Log.Warning($"Track folder not found for {track}");
            return;
        }

        try
        {
            if (!File.Exists(zipPath))
            {
                await Task.Run(() =>
                {
                    using var zipArchive = System.IO.Compression.ZipFile.Open(zipPath, System.IO.Compression.ZipArchiveMode.Create);
                    string[] files = Directory.GetFiles(trackPath, "*", SearchOption.AllDirectories);
                    foreach (string file in files)
                    {
                        string relativePath = file.Substring(trackPath.Length + 1);
                        var entry = zipArchive.CreateEntry(Path.Combine(track, relativePath));
                        if (entry != null)
                        {
                            using var entryStream = entry.Open();
                            using var fileStream = File.OpenRead(file);
                            fileStream.CopyTo(entryStream);
                        }
                    }
                });
                Log.Information($"Zip file creation completed for track {track}");
            }
            else
            {
                Log.Information($"Zip file already exists for track {track}");
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Error creating zip file for track {track}: {ex.Message}");
        }
    }
}
