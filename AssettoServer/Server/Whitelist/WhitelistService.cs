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
    }

    public async Task<bool> IsWhitelistedAsync(ulong guid)
    {
        return (_userGroup is IListableUserGroup listableUserGroup && listableUserGroup.List.Count == 0) || await _userGroup.ContainsAsync(guid);
    }

    public async Task AddAsync(ulong guid)
    {
        await _userGroup.AddAsync(guid);
    }
}
