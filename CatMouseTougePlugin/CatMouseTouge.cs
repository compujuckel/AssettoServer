using System.Reflection;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Plugin;
using AssettoServer.Shared.Services;
using Microsoft.Extensions.Hosting;
using Serilog;
using Microsoft.Data.Sqlite;
using AssettoServer.Network.Tcp;

namespace CatMouseTougePlugin;

public class CatMouseTouge : CriticalBackgroundService, IAssettoServerAutostart
{
    private readonly EntryCarManager _entryCarManager;
    private readonly Func<EntryCar, EntryCarTougeSession> _entryCarTougeSessionFactory;
    private readonly Dictionary<int, EntryCarTougeSession> _instances = [];

    private const string dbPath = "plugins/CatMouseTougePlugin/database.db";

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
            throw new ConfigurationException("CatMouseTougePlugin requires ClientMessages to be enabled");
        }

        var luaPath = Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "lua", "teleport.lua");

        using var streamReader = new StreamReader(luaPath);
        var reconnectScript = streamReader.ReadToEnd();
        scriptProvider.AddScript(reconnectScript, "teleport.lua");

        InitializeDatabase(dbPath);

    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Debug("Cat mouse touge plugin autostart called.");

        foreach (var entryCar in _entryCarManager.EntryCars)
        {
            _instances.Add(entryCar.SessionId, _entryCarTougeSessionFactory(entryCar));
        }

        _entryCarManager.ClientConnected += OnClientConnected;

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
                    CREATE TABLE Players (
                        PlayerId INTEGER PRIMARY KEY,
                        Rating INTEGER,
                        RacesCompleted INTEGER
                    );
            ";
            command.ExecuteNonQuery();
        }
    }

    private void OnClientConnected(ACTcpClient client, EventArgs args)
    {
        // Check if the player is registered in the database
        ulong clientId = client.Guid;

        // Query the database with clientId.
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        // First, check if the player exists
        var checkCommand = connection.CreateCommand();
        checkCommand.CommandText = "SELECT COUNT(*) FROM Players WHERE PlayerId = @PlayerId";
        checkCommand.Parameters.AddWithValue("@PlayerId", (long)clientId);

        int playerExists = Convert.ToInt32(checkCommand.ExecuteScalar());

        // If player doesn't exist, add them with default values
        if (playerExists == 0)
        {
            var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = "INSERT INTO EloRatings (PlayerId, Rating, RacesCompleted) VALUES (@PlayerId, @Rating, @RacesCompleted)";
            insertCommand.Parameters.AddWithValue("@PlayerId", (long)clientId);
            insertCommand.Parameters.AddWithValue("@Rating", 1000); // Default ELO rating
            insertCommand.Parameters.AddWithValue("@RacesCompleted", 0);
            insertCommand.ExecuteNonQuery();

            client.SendChatMessage("Welcome to the server! Your elo is 1000. Improve it by challenging others to a race.");
        }

    }
}

