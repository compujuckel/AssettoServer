using System.Threading.Tasks;

namespace AssettoServer.Server.Whitelist;

public class DefaultWhitelistService : IWhitelistService
{
    private readonly GuidListFile _file;

    public DefaultWhitelistService()
    {
        _file = new GuidListFile("whitelist.txt");
        _ = _file.LoadAsync();
    }
    
    public Task<bool> IsWhitelistedAsync(ulong guid)
    {
        return Task.FromResult(_file.List.Count == 0 || _file.Contains(guid.ToString()));
    }

    public async Task AddAsync(ulong guid)
    {
        await _file.AddAsync(guid.ToString());
    }
}
