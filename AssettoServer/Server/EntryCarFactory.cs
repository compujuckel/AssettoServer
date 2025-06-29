using System;
using System.Linq;
using AssettoServer.Server.Configuration;
using AssettoServer.Shared.Model;

namespace AssettoServer.Server;

public class EntryCarFactory : IEntryCarFactory
{
    public string ClientType => "DEFAULT";

    private readonly EntryCar.Factory _entryCarFactory;
    private readonly ACServerConfiguration _configuration;

    public EntryCarFactory(EntryCar.Factory entryCarFactory, ACServerConfiguration configuration)
    {
        _entryCarFactory = entryCarFactory;
        _configuration = configuration;
    }
    
    public IEntryCar<IClient> Create(IEntry entry, byte sessionId)
    {
        var car = _entryCarFactory(entry.Model, entry.Skin, sessionId);
        
        var driverOptions = CSPDriverOptions.Parse(entry.Skin);
        var aiMode = _configuration.Extra.EnableAi ? entry.AiMode : AiMode.None;
        car.SpectatorMode = entry.SpectatorMode;
        car.Ballast = entry.Ballast;
        car.Restrictor = entry.Restrictor;
        car.FixedSetup = entry.FixedSetup;
        car.DriverOptionsFlags = driverOptions;
        car.AiMode = aiMode;
        car.AiEnableColorChanges = driverOptions.HasFlag(DriverOptionsFlags.AllowColorChange);
        car.AiControlled = aiMode != AiMode.None;
        car.NetworkDistanceSquared = MathF.Pow(_configuration.Extra.NetworkBubbleDistance, 2);
        car.OutsideNetworkBubbleUpdateRateMs = 1000 / _configuration.Extra.OutsideNetworkBubbleRefreshRateHz;
        car.LegalTyres = entry.LegalTyres ?? _configuration.Server.LegalTyres;
        if (!string.IsNullOrWhiteSpace(entry.Guid))
        {
            car.AllowedGuids = entry.Guid.Split(';').Select(ulong.Parse).ToList();
        }
        
        return car;
    }
}
