namespace TougePlugin.Database;

public interface IDatabase
{
    Task UpdatePlayerEloAsync(string playerId, int newElo);
    Task CheckPlayerAsync(string playerId);
    Task<(int Rating, int RacesCompleted)> GetPlayerStatsAsync(string playerId);
}
