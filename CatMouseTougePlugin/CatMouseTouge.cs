using System.Reflection;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Plugin;
using AssettoServer.Shared.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Data.Sqlite;
using AssettoServer.Network.Tcp;
using CatMouseTougePlugin.Packets;

namespace CatMouseTougePlugin;

public class CatMouseTouge : CriticalBackgroundService, IAssettoServerAutostart
{
    private readonly EntryCarManager _entryCarManager;
    private readonly Func<EntryCar, EntryCarTougeSession> _entryCarTougeSessionFactory;
    private readonly Dictionary<int, EntryCarTougeSession> _instances = [];
    private readonly CSPServerScriptProvider _scriptProvider;
    private readonly CSPClientMessageTypeManager _cspClientMessageTypeManager;

    public readonly string dbPath = "plugins/CatMouseTougePlugin/database.db";

    public CatMouseTouge(
        CatMouseTougeConfiguration configuration,
        EntryCarManager entryCarManager,
        Func<EntryCar, EntryCarTougeSession> entryCarTougeSessionFactory,
        IHostApplicationLifetime applicationLifetime,
        CSPServerScriptProvider scriptProvider,
        ACServerConfiguration serverConfiguration,
        CSPClientMessageTypeManager cspClientMessageTypeManager
        ) : base(applicationLifetime)
    {
        _entryCarManager = entryCarManager;
        _entryCarTougeSessionFactory = entryCarTougeSessionFactory;
        _scriptProvider = scriptProvider;
        _cspClientMessageTypeManager = cspClientMessageTypeManager;

        if (!serverConfiguration.Extra.EnableClientMessages)
        {
            throw new ConfigurationException("CatMouseTougePlugin requires ClientMessages to be enabled.");
        }

        // Provide lua scripts
        ProvideScript("teleport.lua");
        ProvideScript("hud.lua");

        InitializeDatabase(dbPath);

        _cspClientMessageTypeManager.RegisterOnlineEvent<EloPacket>(OnEloPacket);
        _cspClientMessageTypeManager.RegisterOnlineEvent<InvitePacket>(OnInvitePacket);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
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
            var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = "INSERT INTO Players (PlayerId, Rating, RacesCompleted) VALUES (@PlayerId, @Rating, @RacesCompleted)";
            insertCommand.Parameters.AddWithValue("@PlayerId", playerId);
            insertCommand.Parameters.AddWithValue("@Rating", 1000); // Default ELO rating
            insertCommand.Parameters.AddWithValue("@RacesCompleted", 0);
            insertCommand.ExecuteNonQuery();

        }
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

    public (int Rating, int RacesCompleted) GetPlayerStats(string playerId)
    {
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
        SELECT Rating, RacesCompleted
        FROM Players
        WHERE PlayerId = $playerId;
    ";
        command.Parameters.AddWithValue("$playerId", playerId);

        using var reader = command.ExecuteReader();

        if (reader.Read())
        {
            int rating = reader.GetInt32(0);           // Rating
            int racesCompleted = reader.GetInt32(1);   // RacesCompleted
            return (rating, racesCompleted);
        }
        else
        {
            throw new Exception($"Player with ID {playerId} not found in the database.");
        }
    }

    private void ProvideScript(string scriptName)
    {
        string scriptPath = Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "lua", scriptName);

        using var streamReader = new StreamReader(scriptPath);
        var reconnectScript = streamReader.ReadToEnd();

        _scriptProvider.AddScript(reconnectScript, scriptName);
    }

    private void OnEloPacket(ACTcpClient client, EloPacket packet)
    {
        int elo = 1000; // Default Elo if player not found
        string playerId = client.Guid.ToString();
        elo = GetPlayerElo(playerId);

        client.SendPacket(new EloPacket { Elo = elo });
    }

    private void OnInvitePacket(ACTcpClient client, InvitePacket packet)
    {
        InviteNearbyCar(client);
    }

    public void InviteNearbyCar(ACTcpClient client)
    {
        EntryCar? nearestCar = GetSession(client!.EntryCar).FindNearbyCar();
        if (nearestCar != null)
        {
            GetSession(client!.EntryCar).ChallengeCar(nearestCar);
        }
        else
        {
            client.SendChatMessage("No car nearby!");
        }
    }
}

