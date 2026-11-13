using System.Threading.Tasks;
using AssettoServer.Network.Tcp;
using AssettoServer.Server.Configuration.Kunos;
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

    public virtual async ValueTask<bool> IsSlotOpenAsync(EntryList.Entry entry, ulong guid)
    {
        if (_nextFilter == null)
            return true;

        return await _nextFilter.IsSlotOpenAsync(entry, guid);
    }

    public virtual Task<AuthFailedResponse?> ShouldAcceptConnectionAsync(ACTcpClient client, HandshakeRequest request)
    {
        return _nextFilter?.ShouldAcceptConnectionAsync(client, request) ?? Task.FromResult<AuthFailedResponse?>(null);
    }
}
