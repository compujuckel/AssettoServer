namespace CatMouseTougePlugin.Database;

public interface IDatabase
{
    Task InitializeDatabase();
    Task UpdatePlayerElo(string playerId, int newElo);
    Task CheckPlayer(string playerId);
    Task<int> GetPlayerElo(string playerId);
    Task<(int Rating, int RacesCompleted)> GetPlayerStats(string playerId);
}
