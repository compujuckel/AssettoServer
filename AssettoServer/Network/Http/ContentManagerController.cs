using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AssettoServer.Server.Configuration;
using AssettoServer.Utils;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace AssettoServer.Network.Http;

[ApiController]
public class ContentManagerController : ControllerBase
{
    private readonly ACServerConfiguration _configuration;

    public ContentManagerController(ACServerConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet("/content/car/{carId}")]
    [HttpHead("/content/car/{carId}")]
    public IActionResult GetCarZip(string carId, string? password = null)
    {
        if (!ValidatePassword(password)) return Unauthorized();
        // if (_configuration.ContentConfiguration?.Cars == null 
        //     || !_configuration.ContentConfiguration.Cars.TryGetValue(carId, out var car)
        //     || car.File == null) return NotFound();

        // return CreateFileDownload(car.File);


        // Rewrite the code to use the existing car data in `content` folder, zip it and return it, by following the instructions
        // 1. check if the car file exists in the `content/cars` folder
        // 2. if it exists, zip it
        // 3. return the zip file
        string carPath = Path.Combine("content", "cars", carId);
        if (!Directory.Exists(carPath)) return NotFound();
        string zipPath = Path.Combine("content", "cars", $"{carId}.zip");
        try
        {
            if (!System.IO.File.Exists(zipPath))
            {
                using (var zipArchive = System.IO.Compression.ZipFile.Open(zipPath, System.IO.Compression.ZipArchiveMode.Create))
                {
                    string[] files = Directory.GetFiles(carPath, "*", SearchOption.AllDirectories);
                    foreach (string file in files)
                    {
                        string relativePath = file.Substring(carPath.Length + 1);
                        var entry = zipArchive.CreateEntry(Path.Combine(carId, relativePath));
                        if (entry != null)
                        {
                            using (var entryStream = entry.Open())
                            using (var fileStream = System.IO.File.OpenRead(file))
                            {
                                fileStream.CopyTo(entryStream);
                            }
                        }
                    }
                }
            }
            return CreateFileDownload(zipPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating or sending zip file for car {carId}: {ex.Message}");
            return StatusCode(500, "Error processing zip file");
        }
        
    }

    [HttpGet("/content/skin/{carId}/{skinId}")]
    [HttpHead("/content/skin/{carId}/{skinId}")]
    public IActionResult GetSkinZip(string carId, string skinId, string? password = null)
    {
        if (!ValidatePassword(password)) return Unauthorized();
        if (_configuration.ContentConfiguration?.Cars == null 
            || !_configuration.ContentConfiguration.Cars.TryGetValue(carId, out var car)
            || car.Skins == null
            || !car.Skins.TryGetValue(skinId, out var skin)
            || skin.File == null) return NotFound();

        return CreateFileDownload(skin.File);
    }

    [HttpGet("/content/track")]
    [HttpHead("/content/track")]
    public IActionResult GetTrackZip(string? password = null)
    {
        if (!ValidatePassword(password)) return Unauthorized();
        // if (_configuration.ContentConfiguration?.Track?.File == null) return NotFound();
        // return CreateFileDownload(_configuration.ContentConfiguration.Track.File);

        string track = _configuration.Server.Track.Substring(_configuration.Server.Track.LastIndexOf('/') + 1);

        string trackPath;
        string zipPath;

        string regularTrackPath = Path.Combine("content", "tracks", track);
        string cspTrackPath = Path.Combine("content", "tracks", "csp", track);

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
            return NotFound();
        }

        
        try
        {
            if (!System.IO.File.Exists(zipPath))
            {
                using (var zipArchive = System.IO.Compression.ZipFile.Open(zipPath, System.IO.Compression.ZipArchiveMode.Create))
                {
                    string[] files = Directory.GetFiles(trackPath, "*", SearchOption.AllDirectories);
                    foreach (string file in files)
                    {
                        string relativePath = file.Substring(trackPath.Length + 1);
                        var entry = zipArchive.CreateEntry(Path.Combine(track, relativePath));
                        if (entry != null)
                        {
                            using (var entryStream = entry.Open())
                            using (var fileStream = System.IO.File.OpenRead(file))
                            {
                                fileStream.CopyTo(entryStream);
                            }
                        }
                    }
                }
            }
            return CreateFileDownload(zipPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating or sending zip file for track {track}: {ex.Message}");
            return StatusCode(500, "Error processing zip file");
        }
    }

    private FileStreamResult CreateFileDownload(string path)
    {
        var fileName = Path.GetFileName(path);
        Stream fileOpen = System.IO.File.OpenRead(path);
        if (_configuration.WrapperParams is not { DownloadSpeedLimit: > 0 })
            return File(fileOpen, "application/zip", fileName);
        
        Stream fileStream = new BandwidthLimitedStream(fileOpen, _configuration.WrapperParams.DownloadSpeedLimit);
        return File(fileStream, "application/zip", fileName);
    }

    private bool ValidatePassword(string? input)
    {
        if (_configuration.Server.Password == null
            || _configuration.WrapperParams is { DownloadPasswordOnly: false }) return true;

        return input != null ?
            Convert.FromHexString(input)
                .SequenceEqual(SHA1.HashData(Encoding.UTF8.GetBytes($"tanidolizedhoatzin{_configuration.Server.Password}")))
            : false;
    }
}
