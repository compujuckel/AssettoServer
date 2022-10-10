using AssettoServer.Server;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace RandomDynamicTrafficPlugin
{
    [ApiController]
    [Route("randomdynamictraffic")]
    public class RandomDynamicTrafficController : ControllerBase
    {
        private readonly ACServer _server;

        public RandomDynamicTrafficController(ACServer server)
        {
            _server = server;
        }

        [HttpGet("/sampleplugin")]
        public string Sample()
        {
            return "Hello from sample plugin!";
        }

        private static readonly string IconsBasePath = Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "icons");

        [HttpGet("accident.png")]
        public IActionResult GetAccidentImage()
        {
            return new PhysicalFileResult(Path.Join(IconsBasePath, "accident.png"), "image/png");
        }

        [HttpGet("casual.png")]
        public IActionResult GetCasualImage()
        {
            return new PhysicalFileResult(Path.Join(IconsBasePath, "casual.png"), "image/png");
        }

        [HttpGet("low.png")]
        public IActionResult GetLowImage()
        {
            return new PhysicalFileResult(Path.Join(IconsBasePath, "low.png"), "image/png");
        }

        [HttpGet("peak.png")]
        public IActionResult GetPeakImage()
        {
            return new PhysicalFileResult(Path.Join(IconsBasePath, "peak.png"), "image/png");
        }
    }
}
