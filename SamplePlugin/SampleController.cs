using AssettoServer.Server;
using Microsoft.AspNetCore.Mvc;

namespace SamplePlugin;

[ApiController]
public class SampleController : ControllerBase
{
    private readonly ACServer _server;
    
    public SampleController(ACServer server)
    {
        _server = server;
    }

    [HttpGet("/sampleplugin")]
    public string Sample()
    {
        return "Hello from sample plugin!";
    }
}