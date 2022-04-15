using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace AutoModerationPlugin;

[ApiController]
[Route("automoderation")]
public class AutoModerationController : ControllerBase
{
    private static readonly string FlagsBasePath = Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Flags");
    
    [HttpGet("wrong_way.png")]
    public IActionResult GetWrongWayImage()
    {
        return new PhysicalFileResult(Path.Join(FlagsBasePath, "wrong_way.png"), "image/png");
    }
    
    [HttpGet("no_parking.png")]
    public IActionResult GetNoParkingImage()
    {
        return new PhysicalFileResult(Path.Join(FlagsBasePath, "no_parking.png"), "image/png");
    }
    
    [HttpGet("no_lights.png")]
    public IActionResult GetNoLightsImage()
    {
        return new PhysicalFileResult(Path.Join(FlagsBasePath, "no_lights.png"), "image/png");
    }
}
