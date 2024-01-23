using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace VotingPresetPlugin;

[ApiController]
[Route("votingpreset")]
public class VotingPresetController : ControllerBase
{
    private static readonly string FlagsBasePath = Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Content");
    
    [HttpGet("reconnecting.png")]
    public IActionResult GetReconnectingImage()
    {
        return new PhysicalFileResult(Path.Join(FlagsBasePath, "reconnecting.png"), "image/png");
    }
}
