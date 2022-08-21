using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssettoServer.Server.Configuration;
using AssettoServer.Utils;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace AssettoServer.Server.Admin;

public class DefaultAdminService : CriticalBackgroundService, IAdminService
{
    private readonly GuidListFile _file;
    private readonly ACServerConfiguration _configuration;

    public DefaultAdminService(ACServerConfiguration configuration, IHostApplicationLifetime applicationLifetime) : base(applicationLifetime)
    {
        _configuration = configuration;
        _file = new GuidListFile("admins.txt");
        _file.Reloaded += OnReloaded;
    }

    private void OnReloaded(GuidListFile sender, EventArgs args)
    {
        // TODO
    }

    public Task<bool> IsAdminAsync(ulong guid)
    {
        return Task.FromResult(_file.Contains(guid.ToString()));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _file.LoadAsync();
        
        if (!_configuration.Extra.UseSteamAuth && _file.List.Any())
        {
            const string errorMsg =
                "Admin whitelist is enabled but Steam auth is disabled. This is unsafe because it allows players to gain admin rights by SteamID spoofing. More info: https://github.com/compujuckel/AssettoServer/wiki/Common-configuration-errors#unsafe-admin-whitelist";
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
