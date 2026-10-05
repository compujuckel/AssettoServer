using System.Collections.Concurrent;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.GeoParams;
using Serilog;
using SystemClock = NodaTime.SystemClock;

namespace ReplayPlugin;

public class ReplayMetadataProvider
{
    public uint Index => _index;
    private uint _index;

    private ConcurrentDictionary<uint, Dictionary<byte, ReplayPlayerInfo>> PlayerInfos { get; } = new();
    
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
            PlayerInfos.Remove(key, out _);
        }
    }

    private void UpdatePlayerInfo()
    {
        try
        {
            var index = Interlocked.Increment(ref _index);

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

            PlayerInfos.TryAdd(index, infos);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error updating replay metadata info");
        }
    }
}
