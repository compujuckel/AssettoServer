using System;
using System.Threading.Tasks;

namespace AssettoServer.Server.Blacklist;

public interface IBlacklistService
{
    public Task<bool> IsBlacklistedAsync(ulong guid);
    public Task AddAsync(ulong guid, string reason = "", ulong? admin = null);

    public event EventHandler<IBlacklistService, BlacklistedEventArgs> Blacklisted;
}

public class BlacklistedEventArgs : EventArgs
{
    public ulong Guid { get; set; }
}