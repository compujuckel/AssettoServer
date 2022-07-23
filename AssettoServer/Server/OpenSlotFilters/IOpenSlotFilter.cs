using System.Threading.Tasks;
using AssettoServer.Network.Packets.Incoming;
using AssettoServer.Network.Packets.Outgoing.Handshake;
using AssettoServer.Network.Tcp;

namespace AssettoServer.Server.OpenSlotFilters;

public interface IOpenSlotFilter
{
    void SetNextFilter(IOpenSlotFilter next);
    bool IsSlotOpen(EntryCar entryCar, ulong guid);
    Task<AuthFailedResponse?> ShouldAcceptConnectionAsync(ACTcpClient client, HandshakeRequest request);
}
