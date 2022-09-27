using System;
using System.Threading.Tasks;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.UserGroup;
using Serilog;

namespace AssettoServer.Server.Admin;

public class AdminService : IAdminService
{
    private readonly IUserGroup _userGroup;
    private readonly ACServerConfiguration _configuration;
    private bool _firstUpdate = true;

    public AdminService(ACServerConfiguration configuration, UserGroupManager userGroupManager)
    {
        _configuration = configuration;
        _userGroup = userGroupManager.Resolve(_configuration.Extra.AdminUserGroup);
        _userGroup.Changed += OnChanged;
    }

    public async Task<bool> IsAdminAsync(ulong guid)
    {
        return await _userGroup.ContainsAsync(guid);
    }

    private void OnChanged(IUserGroup sender, EventArgs args)
    {
        if (!_firstUpdate) return;
        
        _firstUpdate = false;
        if (!_configuration.Extra.UseSteamAuth && _userGroup is IListableUserGroup listableUserGroup && listableUserGroup.List.Count > 0)
        {
            const string errorMsg =
                "Admin whitelist is enabled but Steam auth is disabled. This is unsafe because it allows players to gain admin rights by SteamID spoofing. More info: https://assettoserver.org/docs/common-configuration-errors#unsafe-admin-whitelist";
            if (_configuration.Extra.IgnoreConfigurationErrors.UnsafeAdminWhitelist)
            {
                Log.Warning(errorMsg);
            }
            else
            {
                throw new ConfigurationException(errorMsg);
            }
        }
    }
}
