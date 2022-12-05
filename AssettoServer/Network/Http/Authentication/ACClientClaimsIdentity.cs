using System.Collections.Generic;
using System.Security.Claims;
using AssettoServer.Network.Tcp;

namespace AssettoServer.Network.Http.Authentication;

public class ACClientClaimsIdentity : ClaimsIdentity
{
    public required ACTcpClient Client { get; init; }

    public ACClientClaimsIdentity(IEnumerable<Claim>? claims, string? authenticationType) : base(claims, authenticationType)
    {
    }
}
