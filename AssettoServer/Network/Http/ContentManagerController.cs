using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AssettoServer.Server.Configuration;
using AssettoServer.Utils;
using Microsoft.AspNetCore.Mvc;

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
        if (_configuration.ContentConfiguration?.Cars == null 
            || !_configuration.ContentConfiguration.Cars.TryGetValue(carId, out var car)
            || car.File == null) return NotFound();

        return CreateFileDownload(car.File);
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
        if (_configuration.ContentConfiguration?.Track?.File == null) return NotFound();

        return CreateFileDownload(_configuration.ContentConfiguration.Track.File);
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
