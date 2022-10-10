using AssettoServer.Server;
using Microsoft.AspNetCore.Mvc;

namespace GroupStreetRacingPlugin
{
    [ApiController]
    [Route("groupstreetracing")]
    public class GroupStreetRacingController : ControllerBase
    {
        private readonly ACServer _server;

        public GroupStreetRacingController(ACServer server)
        {
            _server = server;
        }

        [HttpGet("/sampleplugin")]
        public string Sample()
        {
            return "Hello from sample plugin!";
        }
    }
}
