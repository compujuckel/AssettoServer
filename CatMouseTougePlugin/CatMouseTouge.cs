using System.Reflection;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Plugin;
using AssettoServer.Shared.Services;
using Microsoft.Extensions.Hosting;
using Serilog;
using Microsoft.Data.Sqlite;

namespace CatMouseTougePlugin;

public class CatMouseTouge : CriticalBackgroundService, IAssettoServerAutostart
{
    private readonly EntryCarManager _entryCarManager;
    private readonly Func<EntryCar, EntryCarTougeSession> _entryCarTougeSessionFactory;
    private readonly Dictionary<int, EntryCarTougeSession> _instances = [];

    private const string _plugin_folder = "plugins/CatMouseTougePlugin";

    public CatMouseTouge(
        CatMouseTougeConfiguration configuration, 
        EntryCarManager entryCarManager, 
        Func<EntryCar, EntryCarTougeSession> entryCarTougeSessionFactory, 
        IHostApplicationLifetime applicationLifetime,
        CSPServerScriptProvider scriptProvider,
        ACServerConfiguration serverConfiguration
        ) : base(applicationLifetime)
    {
        Log.Debug("UkkO's cat mouse touge plugin called! {Message}", configuration.Message);

        _entryCarManager = entryCarManager;
        _entryCarTougeSessionFactory = entryCarTougeSessionFactory;

        if (!serverConfiguration.Extra.EnableClientMessages)
        {
            throw new ConfigurationException("FastTravelPlugin requires ClientMessages to be enabled");
        }

        var luaPath = Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "lua", "teleport.lua");

        using var streamReader = new StreamReader(luaPath);
        var reconnectScript = streamReader.ReadToEnd();
        scriptProvider.AddScript(reconnectScript, "teleport.lua");

        // Setup SQLite database
        string dbPath = Path.Combine(_plugin_folder, "database.db");
        InitializeDatabase(dbPath);

    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Debug("Cat mouse touge plugin autostart called.");

        foreach (var entryCar in _entryCarManager.EntryCars)
        {
            _instances.Add(entryCar.SessionId, _entryCarTougeSessionFactory(entryCar));
        }

        return Task.CompletedTask;
    }

    internal EntryCarTougeSession GetSession(EntryCar entryCar) => _instances[entryCar.SessionId];

    private void InitializeDatabase(string dbPath)
    {
        if (!File.Exists(dbPath))
        {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText =
            @"
                    CREATE TABLE EloRatings (
                        PlayerId INTEGER PRIMARY KEY,
                        Rating INTEGER,
                        RacesCompleted INTEGER
                    );
            ";
            command.ExecuteNonQuery();
        }
    }
}

