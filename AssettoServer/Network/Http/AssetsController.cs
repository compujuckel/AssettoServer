using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace AssettoServer.Network.Http;

[ApiController]
[Route("assets")]
public class AssetsController : ControllerBase
{
    [HttpGet("logo_42.png")]
    public IActionResult GetLogo42()
    {
        return new FileStreamResult(Assembly.GetExecutingAssembly().GetManifestResourceStream("AssettoServer.Assets.logo_42.png")!, "image/png");
    }
    
    [HttpGet("srp_64.png")]
    public IActionResult GetSRPLogo42()
    {
        return new FileStreamResult(Assembly.GetExecutingAssembly().GetManifestResourceStream("AssettoServer.Assets.srp_64.png")!, "image/png");
    }
}
