using AssettoServer.Server;

namespace ReplayPlugin;

public class ReplayPlayerInfoProvider
{
    public uint Index { get; private set; }
    public Dictionary<uint, Dictionary<byte, ReplayPlayerInfo>> PlayerInfos { get; } = new();
    
    private readonly EntryCarManager _entryCarManager;
    
    public ReplayPlayerInfoProvider(EntryCarManager entryCarManager)
    {
        _entryCarManager = entryCarManager;

        _entryCarManager.ClientConnected += (sender, _) =>
        {
            sender.FirstUpdateSent += (_, _) => UpdatePlayerInfo();
        };
        _entryCarManager.ClientDisconnected += (_, _) => UpdatePlayerInfo();
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
                Guid = client.Guid.ToString()
            });
        }
        
        PlayerInfos.Add(Index, infos);
    }
}
