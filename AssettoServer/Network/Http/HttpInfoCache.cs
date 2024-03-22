using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.GeoParams;
using AssettoServer.Server.Plugin;
using AssettoServer.Shared.Network.Http.Responses;
using AssettoServer.Shared.Services;
using Microsoft.Extensions.Hosting;
using Qommon.Collections.ReadOnly;

namespace AssettoServer.Network.Http;

public class HttpInfoCache : CriticalBackgroundService, IAssettoServerAutostart
{
    private readonly GeoParamsManager _geoParamsManager;
    private readonly EntryCarManager _entryCarManager;

    public IReadOnlyList<string> Cars { get; private set; } = null!;
    public IReadOnlyList<int> Durations { get; }
    public string ServerName { get; }
    public IReadOnlyList<int> SessionTypes { get; }
    public string Track { get; }
    public string PoweredBy { get; }
    public DetailResponseAssists Assists { get; }
    public IReadOnlyList<string> Country { get; private set; } = null!;

    public HttpInfoCache(ACServerConfiguration configuration, EntryCarManager entryCarManager, IHostApplicationLifetime lifetime, GeoParamsManager geoParamsManager) : base(lifetime)
    {
        _entryCarManager = entryCarManager;
        _geoParamsManager = geoParamsManager;
        
        Durations = configuration.Sessions.Select(c => c.IsTimedRace ? c.Time * 60 : c.Laps).ToReadOnlyList();
        SessionTypes = configuration.Sessions.Select(s => (int)s.Type).ToReadOnlyList();
        ServerName = configuration.Server.Name + (configuration.Extra.EnableServerDetails ? " ℹ" + configuration.Server.HttpPort : "");
        Track = configuration.Server.Track + (string.IsNullOrEmpty(configuration.Server.TrackConfig) ? null : "-" + configuration.Server.TrackConfig);
        PoweredBy = $"AssettoServer {configuration.ServerVersion}";
        Assists = new DetailResponseAssists
        {
            AbsState = configuration.Server.ABSAllowed,
            TcState = configuration.Server.TractionControlAllowed,
            FuelRate = (int)(configuration.Server.FuelConsumptionRate * 100),
            DamageMultiplier = (int)(configuration.Server.MechanicalDamageRate * 100),
            TyreWearRate = (int)(configuration.Server.TyreConsumptionRate * 100),
            AllowedTyresOut = configuration.Server.AllowedTyresOutCount,
            StabilityAllowed = configuration.Server.StabilityAllowed,
            AutoclutchAllowed = configuration.Server.AutoClutchAllowed,
            TyreBlanketsAllowed = configuration.Server.AllowTyreBlankets,
            ForceVirtualMirror = configuration.Server.IsVirtualMirrorForced
        };
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Cars = _entryCarManager.EntryCars.Select(c => c.Model).Distinct().ToReadOnlyList();
        Country = new[] { _geoParamsManager.GeoParams.Country, _geoParamsManager.GeoParams.CountryCode };
        return Task.CompletedTask;
    }
}
