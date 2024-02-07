using System.IO;
using AssettoServer.Server.CMContentProviders;
using AssettoServer.Server.Configuration;
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
        var file = System.IO.File.OpenRead(path); 
        var result = File(file, "application/zip", fileName);

        return result;
    }

    private bool ValidatePassword(string input)
    {
        if (_configuration.Server.Password == null) return true;
        return input == _configuration.Server.Password;
    }
}
