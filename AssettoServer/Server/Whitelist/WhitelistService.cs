using System;
using System.Threading.Tasks;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.UserGroup;

namespace AssettoServer.Server.Whitelist;

public class WhitelistService : IWhitelistService
{
    private readonly IUserGroup _userGroup;
    
    public WhitelistService(ACServerConfiguration configuration, UserGroupManager userGroupManager)
    {
        _userGroup = userGroupManager.Resolve(configuration.Extra.WhitelistUserGroup);
        _userGroup.Changed += OnChanged;
    }
    
    private void OnChanged(IUserGroup sender, EventArgs args)
    {
        Changed?.Invoke(this, args);
    }

    public async Task<bool> IsWhitelistedAsync(ulong guid)
    {
        return _userGroup is IListableUserGroup { List.Count: 0 } || await _userGroup.ContainsAsync(guid);
    }

    public async Task AddAsync(ulong guid)
    {
        await _userGroup.AddAsync(guid);
    }
    
    public event EventHandler<IWhitelistService, EventArgs>? Changed;
}
