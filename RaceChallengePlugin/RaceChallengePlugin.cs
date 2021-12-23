using System.Reflection;
using AssettoServer.Server;
using AssettoServer.Server.Plugin;

namespace RaceChallengePlugin;

public class RaceChallengePlugin : IAssettoServerPlugin
{
    internal static readonly Dictionary<int, EntryCarRace> Instances = new();

    public void Initialize(ACServer server)
    {
        if (!server.Features.Contains("CLIENT_MESSAGES"))
        {
            throw new InvalidOperationException("EnableClientMessages and CSP 0.1.76+ are required for this plugin");
        }
        
        server.CSPLuaClientScriptProvider.AddLuaClientScript(File.ReadAllText(Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "lua/racechallenge.lua")));

        foreach (var entryCar in server.EntryCars)
        {
            Instances.Add(entryCar.SessionId, new EntryCarRace(entryCar));
        }
    }
}