using System.IO;
using System.Security.Cryptography;
using System.Text;
using AssettoServer.Server.CMContentProviders;
using AssettoServer.Server.Configuration;
using AssettoServer.Utils;
using Microsoft.AspNetCore.Mvc;

namespace AssettoServer.Network.Http;

[ApiController]
public class ContentManagerController : ControllerBase
{
    private readonly ICMContentProvider _contentProvider;
    private readonly ACServerConfiguration _configuration;

    public ContentManagerController(ICMContentProvider contentProvider, ACServerConfiguration configuration)
    {
        _contentProvider = contentProvider;
        _configuration = configuration;
    }

    [HttpGet("/content/car/{carId}")]
    public IActionResult GetCarZip(string carId, string password = "")
    {
        if (!ValidatePassword(password)) return Unauthorized();
        if (_contentProvider.TryGetZipPath("cars",carId, out var car)) 
            return CreateFileDownload(car);
        
        return NotFound();
    }

    [HttpGet("/content/skin/{carId}/{skinId}")]
    public IActionResult GetSkinZip(string carId, string skinId, string password = "")
    {
        if (!ValidatePassword(password)) return Unauthorized();
        if (_contentProvider.TryGetZipPath("skins",$"{carId}/{skinId}", out var skin)) 
            return CreateFileDownload(skin);
        
        return NotFound();
    }

    [HttpGet("/content/track/{trackId}")]
    public IActionResult GetTrackZip(string trackId, string password = "")
    {
        if (!ValidatePassword(password)) return Unauthorized();
        if (_contentProvider.TryGetZipPath("tracks",trackId, out var track)) 
            return CreateFileDownload(track);
        
        return NotFound();
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

    private bool ValidatePassword(string input)
    {
        if (_configuration.Server.Password == null) return true;
        if (_configuration.WrapperParams?.DownloadPasswordOnly != true) return true;

        var passHash = SHA1.HashData(Encoding.UTF8.GetBytes(@"tanidolizedhoatzin" + _configuration.Server.Password)).ToHexString().ToLowerInvariant();

        return input == passHash;
    }
}
