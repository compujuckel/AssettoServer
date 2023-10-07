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
    
    [HttpGet("srp-logo-new.png")]
    public IActionResult GetSRPLogoNew()
    {
        return new FileStreamResult(Assembly.GetExecutingAssembly().GetManifestResourceStream("AssettoServer.Assets.srp-logo-new.png")!, "image/png");
    }
}
