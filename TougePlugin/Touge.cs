using System.Reflection;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Plugin;
using AssettoServer.Shared.Services;
using Microsoft.Extensions.Hosting;
using AssettoServer.Network.Tcp;
using TougePlugin.Packets;
using TougePlugin.Database;
using System.Numerics;

namespace TougePlugin;

public class Touge : CriticalBackgroundService, IAssettoServerAutostart
{
    private readonly EntryCarManager _entryCarManager;
    private readonly Func<EntryCar, EntryCarTougeSession> _entryCarTougeSessionFactory;
    private readonly Dictionary<int, EntryCarTougeSession> _instances = [];
    private readonly CSPServerScriptProvider _scriptProvider;
    private readonly CSPClientMessageTypeManager _cspClientMessageTypeManager;
    private readonly TougeConfiguration _configuration;
    private readonly IDbConnectionFactory _connectionFactory;

    private static readonly string startingPositionsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cfg", "touge_starting_areas.ini");

    public readonly IDatabase database;
    public readonly Dictionary<string, Vector3>[][] startingPositions;

    public Touge(
        TougeConfiguration configuration,
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
        _configuration = configuration;

        if (!serverConfiguration.Extra.EnableClientMessages)
        {
            throw new ConfigurationException("Touge plugin requires ClientMessages to be enabled.");
        }

        // Provide lua scripts
        ProvideScript("teleport.lua");
        ProvideScript("hud.lua");

        // Set up database connection
        if (_configuration.isDbLocalMode)
        {
            // SQLite database.
            _connectionFactory = new SqliteConnectionFactory("plugins/TougePlugin/database.db");
        }
        else
        {
            // PostgreSQL database.
            _connectionFactory = new PostgresConnectionFactory(_configuration.postgresqlConnectionString!);
        }

        try
        {
            _connectionFactory.InitializeDatabase();
        }
        catch (Exception ex)
        {
            throw new Exception("Failed to initialize touge database: " + ex.Message);
        }

        database = new GenericDatabase(_connectionFactory);

        _cspClientMessageTypeManager.RegisterOnlineEvent<EloPacket>(OnEloPacket);
        _cspClientMessageTypeManager.RegisterOnlineEvent<InvitePacket>(OnInvitePacket);
        _cspClientMessageTypeManager.RegisterOnlineEvent<LobbyStatusPacket>(OnLobbyStatusPacket);

        // Read starting positions from file
        startingPositions = getStartingPositions();
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

    private void OnClientConnected(ACTcpClient client, EventArgs args)
    {
        // Check if the player is registered in the database
        string playerId = client.Guid.ToString();
        database.CheckPlayerAsync(playerId);
    }

    private void ProvideScript(string scriptName)
    {
        string scriptPath = Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "lua", scriptName);

        using var streamReader = new StreamReader(scriptPath);
        var reconnectScript = streamReader.ReadToEnd();

        _scriptProvider.AddScript(reconnectScript, scriptName);
    }

    private async void OnEloPacket(ACTcpClient client, EloPacket packet)
    {
        int elo = 1000; // Default Elo if player not found
        string playerId = client.Guid.ToString();
        elo = await database.GetPlayerEloAsync(playerId);

        client.SendPacket(new EloPacket { Elo = elo });
    }

    private void OnInvitePacket(ACTcpClient client, InvitePacket packet)
    {
        if (packet.InviteSenderName == "nearby")
            InviteNearbyCar(client);
        else if (packet.InviteSenderName == "a")
        {
            // Accept invite.
            var currentSession = GetSession(client.EntryCar).CurrentSession;
            if (currentSession != null && currentSession.Challenger != client.EntryCar && !currentSession.IsActive)
            {
                _ = Task.Run(currentSession.StartAsync);
            }
        }
        else
            // Invite by GUID.
            InviteCar(client, packet.InviteRecipientGuid);
    }

    private void OnLobbyStatusPacket(ACTcpClient client, LobbyStatusPacket packet)
    {
        // Find if there is a player close to the client.
        List<EntryCar>? closestCars = GetSession(client!.EntryCar).FindClosestCars(5);

        List<Dictionary<string, object>> playerStatsList = [];

        foreach (EntryCar car in closestCars)
        {
            Dictionary<string, object> playerStats = new Dictionary<string, object>
            {
                { "name", car.Client!.Name! },
                { "id", car.Client!.Guid! },
                { "inRace", IsInTougeSession(car) },
            };
            playerStatsList.Add(playerStats);
        }

        int nearbyPlayersCount = playerStatsList.Count;
        if (nearbyPlayersCount < 5)
        {
            // Add dummy values to fill up to 5 entries
            for (int i = nearbyPlayersCount; i < 5; i++)
            {
                playerStatsList.Add(new Dictionary<string, object>
                {
                    { "name", "" },
                    { "id", (ulong)0 },
                    { "inRace", false }
                });
            }
        }

        // Send a packet back to the client
        // Close your eyes, this is not going to be pretty :(
        client.SendPacket(new LobbyStatusPacket
        {
            NearbyPlayerName1 = (string)playerStatsList[0]["name"],
            NearbyPlayerId1 = (ulong)playerStatsList[0]["id"],
            NearbyPlayerInRace1 = (bool)playerStatsList[0]["inRace"],
            NearbyPlayerName2 = (string)playerStatsList[1]["name"],
            NearbyPlayerId2 = (ulong)playerStatsList[1]["id"],
            NearbyPlayerInRace2 = (bool)playerStatsList[1]["inRace"],
            NearbyPlayerName3 = (string)playerStatsList[2]["name"],
            NearbyPlayerId3 = (ulong)playerStatsList[2]["id"],
            NearbyPlayerInRace3 = (bool)playerStatsList[2]["inRace"],
            NearbyPlayerName4 = (string)playerStatsList[3]["name"],
            NearbyPlayerId4 = (ulong)playerStatsList[3]["id"],
            NearbyPlayerInRace4 = (bool)playerStatsList[3]["inRace"],
            NearbyPlayerName5 = (string)playerStatsList[4]["name"],
            NearbyPlayerId5 = (ulong)playerStatsList[4]["id"],
            NearbyPlayerInRace5 = (bool)playerStatsList[4]["inRace"],
        });
    }

    private bool IsInTougeSession(EntryCar car)
    {
        EntryCarTougeSession session = GetSession(car);
        if (session.CurrentSession != null)
            return true;
        return false;
    }

    public void InviteNearbyCar(ACTcpClient client)
    {
        EntryCar? nearestCar = GetSession(client!.EntryCar).FindNearbyCar();
        if (nearestCar != null)
        {
            GetSession(client!.EntryCar).ChallengeCar(nearestCar);
            SendNotification(client, "Invite sent!");
        }
        else
        {
            SendNotification(client, "No car nearby!");
        }
    }

    public void InviteCar(ACTcpClient client, ulong recipientId)
    {
        // First find EntryCar in EntryCarManager that matches guid.
        EntryCar? recipientCar = null;
        foreach (EntryCar car in _entryCarManager.EntryCars)
        {
            ACTcpClient? carClient = car.Client;
            if (carClient != null) {
                if (carClient.Guid == recipientId)
                {
                    recipientCar = car;
                    break;
                }
            }
        }

        // Either found the recipient or still null.
        if (recipientCar != null)
        {
            // Invite the recipientCar
            GetSession(client!.EntryCar).ChallengeCar(recipientCar);
            SendNotification(client, "Invite sent!");
        }
        else
        {
            SendNotification(client, "There was an issue sending the invite.");
        }
    }

    internal static void SendNotification(ACTcpClient? client, string message)
    {
        client?.SendPacket(new NotificationPacket { Message = message });
    }

    private Dictionary<string, Vector3>[][] getStartingPositions()
    {
        if (!File.Exists(startingPositionsFile))
        {
            // Create the file
            File.WriteAllText(startingPositionsFile, "[starting_area_1]\nleader_pos =\nleader_heading =\nchaser_pos =\nchaser_heading =");
            throw new Exception("No touge starting areas defined in cfg/touge_starting_areas.ini!");
        }

        return StartingAreaParser.Parse("cfg/touge_starting_areas.ini");
    }
}

