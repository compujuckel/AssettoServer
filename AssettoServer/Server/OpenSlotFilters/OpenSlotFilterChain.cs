using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AssettoServer.Network.Packets.Incoming;
using AssettoServer.Network.Packets.Outgoing.Handshake;
using AssettoServer.Network.Tcp;

namespace AssettoServer.Server.OpenSlotFilters;

public class OpenSlotFilterChain
{
    private readonly IOpenSlotFilter _first;

    public OpenSlotFilterChain(IEnumerable<IOpenSlotFilter> filters)
    {
        IOpenSlotFilter? current = null;
        foreach (var filter in filters)
        {
            _first ??= filter;
            
            current?.SetNextFilter(filter);
            current = filter;
        }

        if (_first == null) throw new InvalidOperationException("No open slot filters set");
    }
    
    public bool IsSlotOpen(EntryCar entryCar, ulong guid)
    {
        return _first.IsSlotOpen(entryCar, guid);
    }

    public Task<AuthFailedResponse?> ShouldAcceptConnectionAsync(ACTcpClient client, HandshakeRequest request)
    {
        return _first.ShouldAcceptConnectionAsync(client, request);
    }
}
