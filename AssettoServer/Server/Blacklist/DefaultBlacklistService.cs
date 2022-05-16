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
        foreach (var entry in _file.List)
        {
            if (entry.Value && ulong.TryParse(entry.Key, out ulong guid))
            {
                Blacklisted?.Invoke(this, new BlacklistedEventArgs { Guid = guid });
            }
        }
    }

    public Task<bool> IsBlacklistedAsync(ulong guid)
    {
        return Task.FromResult(_file.Contains(guid.ToString()));
    }

    public async Task AddAsync(ulong guid, string reason = "", ulong? admin = null)
    {
        await _file.AddAsync(guid.ToString());
        Blacklisted?.Invoke(this, new BlacklistedEventArgs { Guid = guid });
    }

    public event EventHandler<IBlacklistService, BlacklistedEventArgs>? Blacklisted;
}
