using System;
using System.Threading.Tasks;

namespace AssettoServer.Server.Blacklist;

public class DefaultBlacklistService : IBlacklistService
{
    private readonly GuidListFile _file;

    public DefaultBlacklistService()
    {
        _file = new GuidListFile("blacklist.txt");
        _file.Reloaded += OnReloaded;

        _ = _file.LoadAsync();
    }

    private void OnReloaded(GuidListFile sender, EventArgs args)
    {
        Changed?.Invoke(this, args);
    }

    public Task<bool> IsBlacklistedAsync(ulong guid)
    {
        return Task.FromResult(_file.Contains(guid.ToString()));
    }

    public async Task AddAsync(ulong guid, string reason = "", ulong? admin = null)
    {
        await _file.AddAsync(guid.ToString());
    }

    public event EventHandler<IBlacklistService, EventArgs>? Changed;
}
