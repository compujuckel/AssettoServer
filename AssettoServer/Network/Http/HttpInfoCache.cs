using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.GeoParams;
using AssettoServer.Shared.Network.Http.Responses;
using Microsoft.Extensions.Hosting;

namespace AssettoServer.Network.Http;

public class HttpInfoCache : IHostedService
{
    private readonly GeoParamsManager _geoParamsManager;
    private readonly ACServerConfiguration _configuration;

    public IReadOnlyList<string> Cars { get; private set; } = null!;
    public IReadOnlyList<int> Durations { get; }
    public string ServerName { get; }
    public IReadOnlyList<int> SessionTypes { get; }
    public string Track { get; }
    public string PoweredBy { get; }
    public DetailResponseAssists Assists { get; }
    public IReadOnlyList<string> Country { get; private set; } = null!;
    public Dictionary<string, object> Extensions { get; } = [];

    public HttpInfoCache(ACServerConfiguration configuration, GeoParamsManager geoParamsManager)
    {
        _configuration = configuration;
        _geoParamsManager = geoParamsManager;
        
        Durations = configuration.Sessions.Select(c => c.IsTimedRace ? c.Time * 60 : c.Laps).ToList();
        SessionTypes = configuration.Sessions.Select(s => (int)s.Type).ToList();
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

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Cars = _configuration.EntryList.Cars.Select(c => c.Model).Distinct().ToList();
        Country = [_geoParamsManager.GeoParams.Country, _geoParamsManager.GeoParams.CountryCode];
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
