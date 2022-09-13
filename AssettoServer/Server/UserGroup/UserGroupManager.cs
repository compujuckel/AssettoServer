using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using AssettoServer.Server.Configuration;

namespace AssettoServer.Server.UserGroup;

public class UserGroupManager
{
    private readonly IList<IUserGroupProvider> _providers;

    public UserGroupManager(IList<IUserGroupProvider> providers)
    {
        _providers = providers;
    }

    public bool TryResolve(string name, [NotNullWhen(true)] out IUserGroup? group)
    {
        foreach (var provider in _providers)
        {
            group = provider.Resolve(name);
            if (group != null)
            {
                return true;
            }
        }

        group = null;
        return false;
    }

    public IUserGroup Resolve(string name)
    {
        return TryResolve(name, out var group) ? group : throw new ConfigurationException($"No user group found with name {name}");
    }
}
