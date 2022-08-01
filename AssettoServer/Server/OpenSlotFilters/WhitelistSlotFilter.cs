using System.Threading.Tasks;
using AssettoServer.Network.Packets.Incoming;
using AssettoServer.Network.Packets.Outgoing.Handshake;
using AssettoServer.Network.Tcp;
using AssettoServer.Server.Whitelist;

namespace AssettoServer.Server.OpenSlotFilters;

public class WhitelistSlotFilter : OpenSlotFilterBase
{
    private readonly IWhitelistService _whitelist;

    public WhitelistSlotFilter(IWhitelistService whitelist)
    {
        _whitelist = whitelist;
    }

    public override async Task<AuthFailedResponse?> ShouldAcceptConnectionAsync(ACTcpClient client, HandshakeRequest request)
    {
        if (!await _whitelist.IsWhitelistedAsync(request.Guid))
        {
            return new AuthFailedResponse("You are not whitelisted on this server");
        }

        return await base.ShouldAcceptConnectionAsync(client, request);
    }
}
