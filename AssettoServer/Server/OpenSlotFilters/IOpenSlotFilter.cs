using System.Threading.Tasks;
using AssettoServer.Network.Tcp;
using AssettoServer.Shared.Network.Packets.Incoming;
using AssettoServer.Shared.Network.Packets.Outgoing.Handshake;

namespace AssettoServer.Server.OpenSlotFilters;

public interface IOpenSlotFilter
{
    void SetNextFilter(IOpenSlotFilter next);
    ValueTask<bool> IsSlotOpen(EntryCar entryCar, ulong guid);
    Task<AuthFailedResponse?> ShouldAcceptConnectionAsync(ACTcpClient client, HandshakeRequest request);
}
