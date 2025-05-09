using System.Data.Common;
using Npgsql;

namespace CatMouseTougePlugin.Database;

public class PostgresConnectionFactory(string connectionString) : IDbConnectionFactory
{
    public DbConnection CreateConnection()
    {
        return new NpgsqlConnection(connectionString);
    }

    public Task InitializeDatabase()
    {
        // Assume DB exists. Make new table if its missing.
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS Players (
                PlayerId TEXT PRIMARY KEY,
                Rating INTEGER,
                RacesCompleted INTEGER
            );";
        command.ExecuteNonQuery();

        return Task.CompletedTask;
    }
}
