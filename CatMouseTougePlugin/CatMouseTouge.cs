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

    public readonly string dbPath = "plugins/CatMouseTougePlugin/database.db";

    public CatMouseTouge(
        CatMouseTougeConfiguration configuration,
        EntryCarManager entryCarManager,
        Func<EntryCar, EntryCarTougeSession> entryCarTougeSessionFactory,
        IHostApplicationLifetime applicationLifetime,
        CSPServerScriptProvider scriptProvider,
        ACServerConfiguration serverConfiguration
        ) : base(applicationLifetime)
    {
        Log.Debug("Starting UkkO's cat mouse touge plugin.!");

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
                        PlayerId TEXT PRIMARY KEY,
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
        string playerId = client.Guid.ToString();

        // Query the database with clientId.
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        // First, check if the player exists
        var checkCommand = connection.CreateCommand();
        checkCommand.CommandText = "SELECT COUNT(*) FROM Players WHERE PlayerId = @PlayerId";
        checkCommand.Parameters.AddWithValue("@PlayerId", playerId);

        int playerExists = Convert.ToInt32(checkCommand.ExecuteScalar());

        // If player doesn't exist, add them with default values
        if (playerExists == 0)
        {
            Log.Debug("Player does not exist. Adding to database.");
            var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = "INSERT INTO Players (PlayerId, Rating, RacesCompleted) VALUES (@PlayerId, @Rating, @RacesCompleted)";
            insertCommand.Parameters.AddWithValue("@PlayerId", playerId);
            insertCommand.Parameters.AddWithValue("@Rating", 1000); // Default ELO rating
            insertCommand.Parameters.AddWithValue("@RacesCompleted", 0);
            insertCommand.ExecuteNonQuery();

        }
        else
        {
            Log.Debug("Player already exists in database.");
            //client.SendChatMessage("Welcome back!");
        }

        client.FirstUpdateSent += OnFirstUpdate;

    }

    private void OnFirstUpdate(ACTcpClient client, EventArgs args)
    {
        int elo = 1000; // Default Elo if player not found
        string playerId = client.Guid.ToString();

        elo = GetPlayerElo(playerId);

        client.SendChatMessage($"Welcome to the server, your touge elo is {elo}. Improve it by racing other players!");
    }

    public int GetPlayerElo(string playerId)
    {
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
        SELECT Rating
        FROM Players
        WHERE PlayerId = $playerId
    ";
        command.Parameters.AddWithValue("$playerId", playerId);

        var result = command.ExecuteScalar();

        if (result != null && int.TryParse(result.ToString(), out int rating))
        {
            return rating;
        }
        else
        {
            throw new Exception($"Player with ID {playerId} not found in the database.");
        }
    }
}

