using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace CyclePresetPlugin;

[ApiController]
[Route("cyclepreset")]
public class CyclePresetController : ControllerBase
{
    private static readonly string FlagsBasePath = Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Content");
    
    [HttpGet("reconnecting.png")]
    public IActionResult GetWrongWayImage()
    {
        return new PhysicalFileResult(Path.Join(FlagsBasePath, "reconnecting.png"), "image/png");
    }
}
