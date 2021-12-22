using Microsoft.AspNetCore.Mvc;

namespace RaceChallengePlugin;

[ApiController]
public class ScriptController : ControllerBase
{
    [HttpGet("/script")]
    public string GetScript()
    {
        return System.IO.File.ReadAllText(@"C:\Program Files (x86)\Steam\steamapps\common\assettocorsa\extension\config\tracks\test.lua");
    }
}