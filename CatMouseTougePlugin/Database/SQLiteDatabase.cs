using Microsoft.Data.Sqlite;

namespace CatMouseTougePlugin.Database;

public class SQLiteDatabase(string dbPath) : IDatabase
{
    public Task InitializeDatabase()
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
        return Task.CompletedTask;
    }
    
    public Task UpdatePlayerElo(string playerId, int newElo)
    {
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
        UPDATE Players
        SET Rating = $newRating,
            RacesCompleted = RacesCompleted + 1
        WHERE PlayerId = $playerId;
        ";
        command.Parameters.AddWithValue("$newRating", newElo);
        command.Parameters.AddWithValue("$playerId", playerId);

        int affectedRows = command.ExecuteNonQuery();

        if (affectedRows == 0)
        {
            throw new Exception($"Failed to update rating. Player with ID {playerId} not found.");
        }

        return Task.CompletedTask;
    }

    public Task CheckPlayer(string playerId)
    {
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

        return Task.CompletedTask;
    }

    public Task<int> GetPlayerElo(string playerId)
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
            return Task.FromResult(rating);
        }
        else
        {
            throw new Exception($"Player with ID {playerId} not found in the database.");
        }
    }

    public Task<(int Rating, int RacesCompleted)> GetPlayerStats(string playerId)
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
            return Task.FromResult((rating, racesCompleted));
        }
        else
        {
            throw new Exception($"Player with ID {playerId} not found in the database.");
        }
    }
}
