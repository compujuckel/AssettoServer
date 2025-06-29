using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AssettoServer.Network.Http.Authentication;

public class ACClientAuthenticationHandler : AuthenticationHandler<ACClientAuthenticationSchemeOptions>
{
    private const string CarIdHeader = "X-Car-Id";
    private const string ApiKeyHeader = "X-Api-Key";
    
    private readonly EntryCarManager _entryCarManager;
    
    public ACClientAuthenticationHandler(IOptionsMonitor<ACClientAuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, EntryCarManager entryCarManager) : base(options, logger, encoder)
    {
        _entryCarManager = entryCarManager;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(CarIdHeader, out var carIdHdr) 
            || !Request.Headers.TryGetValue(ApiKeyHeader, out var apiKeyHdr))
        {
            return Task.FromResult(AuthenticateResult.Fail("Header Not Found."));
        }

        if (!int.TryParse(carIdHdr, out var carId))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid car id."));
        }
        var apiKey = apiKeyHdr.ToString();

        if (_entryCarManager.EntryCars[carId].Client is ACTcpClient client && client.ApiKey == apiKey)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, client.Guid.ToString()),
                new(ClaimTypes.Name, client.Name!)
            };

            if (client is { IsAdministrator: true })
            {
                claims.Add(new Claim(ClaimTypes.Role, "Administrator"));
            }

            var claimsIdentity = new ACClientClaimsIdentity(claims, nameof(ACClientAuthenticationHandler)) { Client = client };
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(claimsIdentity), Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
        
        return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
    }
}
