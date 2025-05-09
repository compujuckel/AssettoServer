using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace CatMouseTougePlugin.Database;

public class SqliteConnectionFactory : IDbConnectionFactory
{
    private readonly string _dbPath;
    public SqliteConnectionFactory(string dbPath)
    {
        _dbPath = dbPath;
    }
    public DbConnection CreateConnection()
    {
        return new SqliteConnection($"Data Source={_dbPath}");
    }

    public Task InitializeDatabase()
    {
        if (!File.Exists(_dbPath))
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
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
}
