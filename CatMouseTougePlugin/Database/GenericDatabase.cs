namespace CatMouseTougePlugin.Database;

public class GenericDatabase(IDbConnectionFactory factory) : IDatabase
{
    public async Task UpdatePlayerEloAsync(string playerId, int newElo)
    {
        await using var connection = factory.CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
        UPDATE Players
        SET Rating = $newRating,
            RacesCompleted = RacesCompleted + 1
        WHERE PlayerId = $playerId;
        ";

        command.AddParameter("$newRating", newElo);
        command.AddParameter("$playerId", playerId);

        int affectedRows = await command.ExecuteNonQueryAsync();

        if (affectedRows == 0)
        {
            throw new Exception($"Failed to update rating. Player with ID {playerId} not found.");
        }
    }

    public async Task CheckPlayerAsync(string playerId)
    {
        // Query the database with clientId.
        await using var connection = factory.CreateConnection();
        connection.Open();

        // First, check if the player exists
        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Players WHERE PlayerId = @PlayerId";
        command.AddParameter("@PlayerId", playerId);

        int playerExists = Convert.ToInt32(command.ExecuteScalar());

        // If player doesn't exist, add them with default values
        if (playerExists == 0)
        {
            var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = "INSERT INTO Players (PlayerId, Rating, RacesCompleted) VALUES (@PlayerId, @Rating, @RacesCompleted)";
            insertCommand.AddParameter("@PlayerId", playerId);
            insertCommand.AddParameter("@Rating", 1000); // Default ELO rating
            insertCommand.AddParameter("@RacesCompleted", 0);
            insertCommand.ExecuteNonQuery();
        }
    }

    public async Task<int> GetPlayerEloAsync(string playerId)
    {
        await using var connection = factory.CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
        SELECT Rating
        FROM Players
        WHERE PlayerId = $playerId
        ";
        command.AddParameter("$playerId", playerId);

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

    public async Task<(int Rating, int RacesCompleted)> GetPlayerStatsAsync(string playerId)
    {
        await using var connection = factory.CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
        SELECT Rating, RacesCompleted
        FROM Players
        WHERE PlayerId = $playerId;
        ";
        command.AddParameter("$playerId", playerId);

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
}
