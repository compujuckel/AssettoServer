using System;
using System.Threading.Tasks;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.UserGroup;

namespace AssettoServer.Server.Blacklist;

public class BlacklistService : IBlacklistService
{
    private readonly IUserGroup _userGroup;

    public BlacklistService(ACServerConfiguration configuration, UserGroupManager userGroupManager)
    {
        _userGroup = userGroupManager.Resolve(configuration.Extra.BlacklistUserGroup);
        _userGroup.Changed += OnChanged;
    }

    private void OnChanged(IUserGroup sender, EventArgs args)
    {
        Changed?.Invoke(this, args);
    }

    public async Task<bool> IsBlacklistedAsync(ulong guid)
    {
        return await _userGroup.ContainsAsync(guid);
    }

    public async Task AddAsync(ulong guid, string reason = "", ulong? admin = null)
    {
        await _userGroup.AddAsync(guid);
    }

    public event EventHandler<IBlacklistService, EventArgs>? Changed;
}
