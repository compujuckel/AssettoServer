using System.Threading.Tasks;
using AssettoServer.Network.Tcp;
using AssettoServer.Shared.Network.Packets.Incoming;
using AssettoServer.Shared.Network.Packets.Outgoing.Handshake;

namespace AssettoServer.Server.OpenSlotFilters;

public abstract class OpenSlotFilterBase : IOpenSlotFilter
{
    private IOpenSlotFilter? _nextFilter;
    
    public void SetNextFilter(IOpenSlotFilter next)
    {
        _nextFilter = next;
    }

    public virtual bool IsSlotOpen(EntryCar entryCar, ulong guid)
    {
        return _nextFilter?.IsSlotOpen(entryCar, guid) ?? true;
    }

    public virtual Task<AuthFailedResponse?> ShouldAcceptConnectionAsync(ACTcpClient client, HandshakeRequest request)
    {
        return _nextFilter?.ShouldAcceptConnectionAsync(client, request) ?? Task.FromResult<AuthFailedResponse?>(null);
    }
}
