using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.GeoParams;
using SystemClock = NodaTime.SystemClock;

namespace ReplayPlugin;

public class ReplayMetadataProvider
{
    public uint Index { get; private set; }
    public Dictionary<uint, Dictionary<byte, ReplayPlayerInfo>> PlayerInfos { get; } = new();
    
    private readonly EntryCarManager _entryCarManager;
    private readonly ACServerConfiguration _configuration;
    private readonly GeoParamsManager _geoParamsManager;
    
    public ReplayMetadataProvider(EntryCarManager entryCarManager, ACServerConfiguration configuration, GeoParamsManager geoParamsManager)
    {
        _entryCarManager = entryCarManager;
        _configuration = configuration;
        _geoParamsManager = geoParamsManager;

        _entryCarManager.ClientConnected += (sender, _) =>
        {
            sender.FirstUpdateSent += (_, _) => UpdatePlayerInfo();
        };
        _entryCarManager.ClientDisconnected += (_, _) => UpdatePlayerInfo();
    }

    public ReplayMetadata GenerateMetadata()
    {
        return new ReplayMetadata
        {
            ServerName = _configuration.Server.Name,
            ServerAddress = $"{_geoParamsManager.GeoParams.Ip}:{_configuration.Server.HttpPort}",
            Timestamp = SystemClock.Instance.GetCurrentInstant().ToUnixTimeSeconds().ToString(),
            PlayerInfos = PlayerInfos
        };
    }

    public void Cleanup(uint before)
    {
        var toDelete = PlayerInfos.Keys.Where(key => key < before);
        foreach (var key in toDelete)
        {
            PlayerInfos.Remove(key);
        }
    }

    private void UpdatePlayerInfo()
    {
        Index++;

        var infos = new Dictionary<byte, ReplayPlayerInfo>();
        foreach (var car in _entryCarManager.ConnectedCars)
        {
            var client = car.Value.Client;
            if (client == null) continue;
            
            infos.Add((byte)car.Key, new ReplayPlayerInfo
            {
                Name = client.Name ?? "",
                Guid = client.Guid.ToString(),
                OwnerGuid = client.OwnerGuid?.ToString(),
                NationCode = client.NationCode
            });
        }
        
        PlayerInfos.Add(Index, infos);
    }
}
