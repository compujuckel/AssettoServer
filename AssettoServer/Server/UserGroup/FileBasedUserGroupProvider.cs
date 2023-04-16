using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AssettoServer.Server.Configuration;
using AssettoServer.Shared.Services;
using AssettoServer.Utils;
using Microsoft.Extensions.Hosting;

namespace AssettoServer.Server.UserGroup;

public class FileBasedUserGroupProvider : CriticalBackgroundService, IUserGroupProvider
{
    private readonly Dictionary<string, FileBasedUserGroup> _userGroups = new();

    public FileBasedUserGroupProvider(ACServerConfiguration configuration, FileBasedUserGroup.Factory fileBasedUserGroupFactory, IHostApplicationLifetime lifetime) : base(lifetime)
    {
        foreach ((string name, string path) in configuration.Extra.UserGroups)
        {
            _userGroups.Add(name, fileBasedUserGroupFactory(name, path));
        }
    }

    public IUserGroup? Resolve(string name)
    {
        return _userGroups.TryGetValue(name, out var group) ? group : null;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        foreach (var group in _userGroups.Values)
        {
            await group.LoadAsync();
        }
    }
}
