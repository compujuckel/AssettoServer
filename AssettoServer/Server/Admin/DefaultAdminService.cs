using System;
using System.Threading.Tasks;

namespace AssettoServer.Server.Admin;

public class DefaultAdminService : IAdminService
{
    private readonly GuidListFile _file;

    public DefaultAdminService()
    {
        _file = new GuidListFile("admins.txt");
        _file.Reloaded += OnReloaded;

        _ = _file.LoadAsync();
    }

    private void OnReloaded(GuidListFile sender, EventArgs args)
    {
        // TODO
    }

    public Task<bool> IsAdminAsync(ulong guid)
    {
        return Task.FromResult(_file.Contains(guid.ToString()));
    }
}
