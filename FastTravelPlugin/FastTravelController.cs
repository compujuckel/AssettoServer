using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace FastTravelPlugin;

[ApiController]
[Route("fasttravel")]
public class FastTravelController : ControllerBase
{
    private static readonly string FlagsBasePath = Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Content");
    
    [HttpGet("cursor_ch.png")]
    public IActionResult GetCursorChImage()
    {
        return new PhysicalFileResult(Path.Join(FlagsBasePath, "cursor_ch.png"), "image/png");
    }
    
    [HttpGet("cursor_ng.png")]
    public IActionResult GetCursorNgImage()
    {
        return new PhysicalFileResult(Path.Join(FlagsBasePath, "cursor_ng.png"), "image/png");
    }
    
    [HttpGet("cursor_player.png")]
    public IActionResult GetCursorPlayerImage()
    {
        return new PhysicalFileResult(Path.Join(FlagsBasePath, "cursor_player.png"), "image/png");
    }
    
    [HttpGet("cursor_std.png")]
    public IActionResult GetCursorStdImage()
    {
        return new PhysicalFileResult(Path.Join(FlagsBasePath, "cursor_std.png"), "image/png");
    }
    
    [HttpGet("mapicon_pa.png")]
    public IActionResult GetMapIconPaImage()
    {
        return new PhysicalFileResult(Path.Join(FlagsBasePath, "mapicon_pa.png"), "image/png");
    }
    
    [HttpGet("mapicon_sp.png")]
    public IActionResult GetMapIconSpImage()
    {
        return new PhysicalFileResult(Path.Join(FlagsBasePath, "mapicon_sp.png"), "image/png");
    }
    
    [HttpGet("mapicon_st.png")]
    public IActionResult GetMapIconStImage()
    {
        return new PhysicalFileResult(Path.Join(FlagsBasePath, "mapicon_st.png"), "image/png");
    }
    
    [HttpGet("map.png")]
    public IActionResult GetMapImage()
    {
        return new PhysicalFileResult(Path.Join(FlagsBasePath, "map.png"), "image/png");
    }
}

