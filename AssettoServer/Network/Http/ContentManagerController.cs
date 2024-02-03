using System.IO;
using AssettoServer.Server.CMContentProviders;
using AssettoServer.Server.Configuration;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace AssettoServer.Network.Http;

[ApiController]
public class ContentManagerController : ControllerBase
{
    private readonly ACServerConfiguration _configuration;
    private readonly ICMContentProvider _contentProvider;

    public ContentManagerController(ACServerConfiguration configuration, ICMContentProvider contentProvider)
    {
        _configuration = configuration;
        _contentProvider = contentProvider;
    }

    [EnableCors("ServerQueryPolicy")]
    [HttpGet("/content/car/{carId}")]
    public IActionResult GetCarZip(string carId)
    {
        if (_contentProvider.TryGetZipPath("cars",carId, out var car)) 
            return CreateFileDownload(car);
        
        return NotFound();
    }

    [EnableCors("ServerQueryPolicy")]
    [HttpGet("/content/skin/{carId}/{skinId}")]
    public IActionResult GetSkinZip(string carId, string skinId)
    {
        if (_contentProvider.TryGetZipPath("skins",$"{carId}/{skinId}", out var skin)) 
            return CreateFileDownload(skin);
        
        return NotFound();
    }

    [EnableCors("ServerQueryPolicy")]
    [HttpGet("/content/track/{trackId}")]
    public IActionResult GetTrackZip(string trackId)
    {
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
}
