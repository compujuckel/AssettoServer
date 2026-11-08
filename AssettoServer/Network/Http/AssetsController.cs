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
        return new FileStreamResult(EmbeddedResources.Images.Logo_42.Png.GetStream(), "image/png");
    }
    
    [HttpGet("srp-logo-new.png")]
    public IActionResult GetSRPLogoNew()
    {
        return new FileStreamResult(EmbeddedResources.Images.Srp_logo_new.Png.GetStream(), "image/png");
    }
}
