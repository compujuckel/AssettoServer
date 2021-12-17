using AssettoServer.Server;
using AssettoServer.Server.Plugin;

namespace RaceChallengePlugin;

public class RaceChallengePlugin : IAssettoServerPlugin
{
    internal static readonly Dictionary<int, EntryCarRace> Instances = new();
    
    public void Initialize(ACServer server)
    {
        foreach (var entryCar in server.EntryCars)
        {
            Instances.Add(entryCar.SessionId, new EntryCarRace(entryCar));
        }
    }
}