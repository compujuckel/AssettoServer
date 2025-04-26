
using System.ComponentModel.DataAnnotations;
using AssettoServer.Server;
using AssettoServer.Shared.Model;
using Microsoft.Data.Sqlite;
using Serilog;

namespace CatMouseTougePlugin;

public class TougeSession
{
    public EntryCar Challenger { get; }
    public EntryCar Challenged { get; }

    public bool IsActive { get; private set; }

    private readonly SessionManager _sessionManager;
    private readonly EntryCarManager _entryCarManager;
    private readonly CatMouseTouge _plugin;
    private readonly Race.Factory _raceFactory;

    public delegate TougeSession Factory(EntryCar challenger, EntryCar challenged);

    public TougeSession(EntryCar challenger, EntryCar challenged, SessionManager sessionManager, EntryCarManager entryCarManager, CatMouseTouge plugin, Race.Factory raceFactory)
    {
        Challenger = challenger;
        Challenged = challenged;
        _sessionManager = sessionManager;
        _entryCarManager = entryCarManager;
        _plugin = plugin;
        _raceFactory = raceFactory;
    }

    public Task StartAsync()
    {
        if(!IsActive)
        {
            IsActive = true;
            _ = Task.Run(TougeSessionAsync);
        }

        return Task.CompletedTask;
    }

    private async Task TougeSessionAsync()
    {
        try
        {
            EntryCar? overallWinner = null;
            // Run race 1.
            Race race1 = _raceFactory(Challenger, Challenged);
            EntryCar? winner1 = await race1.RaceAsync();
            // Because its important the race is finished before starting the next one.
            if (winner1 != null)
            {
                Race race2 = _raceFactory(Challenged, Challenger);
                EntryCar? winner2 = await race2.RaceAsync();

                if (winner2 != null)
                {
                    if (winner1 != winner2)
                    {
                        Race race3 = _raceFactory(Challenger, Challenged);
                        overallWinner = await race3.RaceAsync(); // The overall winner.
                    }
                    else
                    {
                        overallWinner = winner1;
                    }
                }
            }
            UpdateElo(overallWinner);
        }
        catch (Exception ex) 
        {
            Log.Error(ex, "Error while running touge session.");
        }
        finally
        {
            FinishTougeSession();
        }
    }

    private void FinishTougeSession()
    {
        _plugin.GetSession(Challenger).CurrentSession = null;
        _plugin.GetSession(Challenged).CurrentSession = null;

        string ChallengedName = Challenged.Client?.Name!;
        string ChallengerName = Challenger.Client?.Name!;

        _entryCarManager.BroadcastChat($"Race between {ChallengerName} and {ChallengedName} has concluded!");
    }

    private void UpdateElo(EntryCar? winner)
    {
        if (winner == null) return;
        var loser = (winner == Challenger) ? Challenged : Challenger;

        string winnerId = winner.Client!.Guid.ToString();
        string loserId = loser.Client!.Guid.ToString();

        int winnerElo = _plugin.GetPlayerElo(winnerId);
        int loserElo = _plugin.GetPlayerElo(loserId);

        // Actually do elo calcs.
        int winnerCarRating = 700; //Hardcoded for now, later retrieve from cfg.
        int loserCarRating = 700;

        int newWinnerElo = CalculateElo(winnerElo, loserElo, winnerCarRating, loserCarRating, true);
        int newLoserElo = CalculateElo(loserElo, winnerElo, loserCarRating, winnerCarRating, false);

        // Update elo in the database.
        UpdatePlayerElo(winnerId, newWinnerElo);
        UpdatePlayerElo(loserId, newLoserElo);
    }

    private int CalculateElo(int playerElo, int opponentElo, int playerCarRating, int opponentCarRating, bool hasWon)
    {
        // Calculate car performance difference factor
        double carAdvantage = (playerCarRating - opponentCarRating) / 100;

        // Adjust effective ratings based on car performance.
        double effectivePlayerElo = playerElo - carAdvantage * 100;

        // Calculate expected outcome (standard ELO formula)
        double expectedResult = 1.0 / (1.0 + Math.Pow(10.0, (opponentElo - effectivePlayerElo / 400.0)));

        int maxGain = 32; //Hardcoded for now, later retrieve from cfg.

        // Calculate base ELO change
        int result = hasWon ? 1 : 0;
        double eloChange = maxGain * (result - expectedResult);

        // Apply car performance adjustment to ELO change
        // If player has better car (positive car_advantage), reduce gains and increase losses
        // If player has worse car (negative car_advantage), increase gains and reduce losses
        double carFactor = 1 - (carAdvantage * 0.5);

        // Ensure car_factor is within reasonable bounds (0.5 to 1.5)
        carFactor = Math.Max(0.5, Math.Min(1.5, carFactor));

        // Apply car factor to elo change.
        int adjustedEloChange = (int)Math.Round(eloChange * carFactor);

        return playerElo + adjustedEloChange;

    }

    private void UpdatePlayerElo(string playerId, int newElo)
    {
        using var connection = new SqliteConnection($"Data Source={_plugin.dbPath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
        UPDATE Players
        SET Rating = $newRating
        WHERE PlayerId = $playerId
    ";
        command.Parameters.AddWithValue("$newRating", newElo);
        command.Parameters.AddWithValue("$playerId", playerId);

        int affectedRows = command.ExecuteNonQuery();

        if (affectedRows == 0)
        {
            throw new Exception($"Failed to update rating. Player with ID {playerId} not found.");
        }
    }
}
