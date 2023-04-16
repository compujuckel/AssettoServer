using System.Threading.Tasks;
using AssettoServer.Network.Tcp;
using AssettoServer.Shared.Network.Packets.Incoming;
using AssettoServer.Shared.Network.Packets.Outgoing.Handshake;

namespace AssettoServer.Server.OpenSlotFilters;

public class SteamSlotFilter : OpenSlotFilterBase
{
    private readonly Steam _steam;

    public SteamSlotFilter(Steam steam)
    {
        _steam = steam;
    }

    public override async Task<AuthFailedResponse?> ShouldAcceptConnectionAsync(ACTcpClient client, HandshakeRequest request)
    {
        if (!await _steam.ValidateSessionTicketAsync(request.SessionTicket, request.Guid, client))
        {
            return new AuthFailedResponse("Steam authentication failed.");
        }
        
        return await base.ShouldAcceptConnectionAsync(client, request);
    }
}
