using System;
using System.Threading.Tasks;

namespace AssettoServer.Server.Whitelist;

public interface IWhitelistService
{
    public Task<bool> IsWhitelistedAsync(ulong guid);
    public Task AddAsync(ulong guid);
    
    public event EventHandler<IWhitelistService, EventArgs> Changed;
}
