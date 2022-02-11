using System.Net;
using AssettoServer.Network.Packets.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace ReportPlugin;

[ApiController]
public class ReportController : ControllerBase
{
    [HttpPost("/report")]
    public async Task<ActionResult> PostReport(Guid key, [FromHeader(Name = "X-Car-Id")] int sessionId)
    {
        var plugin = ReportPluginHolder.Instance;
        var reporterClient = plugin.Server.EntryCars[sessionId].Client ?? throw new InvalidOperationException("Client not connected");
        var lastReport = reporterClient.GetLastReplay();

        if (plugin.Key != key
            || !(IPAddress.IsLoopback(Request.HttpContext.Connection.RemoteIpAddress!) || Equals((reporterClient.TcpClient.Client.RemoteEndPoint as IPEndPoint)?.Address, Request.HttpContext.Connection.RemoteIpAddress)))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        if (lastReport?.AuditLog?.Timestamp > DateTime.UtcNow - TimeSpan.FromSeconds(30))
        {
            reporterClient.SendPacket(new ChatMessage {SessionId = 255, Message = "Please wait a moment before submitting another replay."});
            return StatusCode(StatusCodes.Status429TooManyRequests);
        }
        
        var ts = DateTime.UtcNow;

        var guid = Guid.NewGuid();
        await using (var file = System.IO.File.Create(Path.Join("reports", $"{guid}.zip")))
        {
            await Request.Body.CopyToAsync(file);
        }

        var auditLog = plugin.GetAuditLog(ts);
        string serialized = JsonConvert.SerializeObject(auditLog, Formatting.Indented);
        await System.IO.File.WriteAllTextAsync(Path.Join("reports", $"{guid}.json"), serialized);

        var report = new Replay(guid, auditLog);
        reporterClient.SetLastReplay(report);

        reporterClient.Logger.Information("Replay received from {ClientName} ({SessionId}), ID: {Id}", reporterClient.Name, reporterClient.SessionId, guid);
        reporterClient.SendPacket(new ChatMessage {SessionId = 255, Message = "Replay received.\nUse /report <reason> to submit this replay to moderators."});
        
        return Ok();
    }
}
