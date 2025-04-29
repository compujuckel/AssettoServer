
using AssettoServer.Server;
using CatMouseTougePlugin.Packets;
using Microsoft.Data.Sqlite;
using Serilog;

namespace CatMouseTougePlugin;

public class TougeSession
{
    public EntryCar Challenger { get; }
    public EntryCar Challenged { get; }

    private int[] challengerStandings = [(int)RaceResult.Tbd, (int)RaceResult.Tbd, (int)RaceResult.Tbd];
    private int[] challengedStandings = [(int)RaceResult.Tbd, (int)RaceResult.Tbd, (int)RaceResult.Tbd];

    private enum RaceResult
    {
        Tbd = 0,
        Win = 1,
        Loss = 2,
    }

    public bool IsActive { get; private set; }

    private readonly EntryCarManager _entryCarManager;
    private readonly CatMouseTouge _plugin;
    private readonly Race.Factory _raceFactory;
    private readonly CatMouseTougeConfiguration _configuration;

    public delegate TougeSession Factory(EntryCar challenger, EntryCar challenged);

    public TougeSession(EntryCar challenger, EntryCar challenged, EntryCarManager entryCarManager, CatMouseTouge plugin, Race.Factory raceFactory, CatMouseTougeConfiguration configuration)
    {
        Challenger = challenger;
        Challenged = challenged;
        _entryCarManager = entryCarManager;
        _plugin = plugin;
        _raceFactory = raceFactory;
        _configuration = configuration;
    }

    public Task StartAsync()
    {
        if (!IsActive)
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

            // Turn on the hud
            SendStandings(true);

            // Run race 1.
            Race race1 = _raceFactory(Challenger, Challenged);
            EntryCar? winner1 = await race1.RaceAsync();

            // If there is a winner in race 1, run race 2.
            if (winner1 != null)
            {
                // Update the standings for both players on client side.
                UpdateStandings(winner1, 1);
                // Start second race.
                Race race2 = _raceFactory(Challenged, Challenger);
                EntryCar? winner2 = await race2.RaceAsync();

                if (winner2 != null)
                {
                    UpdateStandings(winner2, 2);
                    if (winner1 != winner2)
                    {
                        Race race3 = _raceFactory(Challenger, Challenged);
                        EntryCar? winner3 = await race3.RaceAsync(); // The overall winner.
                        if (winner3 != null)
                        {
                            UpdateStandings(winner3, 3);
                            overallWinner = winner3;
                        }
                        else
                        {
                            overallWinner = null;
                        }
                    }
                    else
                    {
                        overallWinner = winner1;
                    }
                }
                if (overallWinner != null)
                {
                    UpdateElo(overallWinner);
                }
            }
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

        // Turn off and reset hud
        Array.Fill(challengerStandings, (int)RaceResult.Tbd);
        Array.Fill(challengedStandings, (int)RaceResult.Tbd);
        SendStandings(false);
    }

    private void UpdateElo(EntryCar? winner)
    {
        if (winner == null) return;
        var loser = (winner == Challenger) ? Challenged : Challenger;

        string winnerId = winner.Client!.Guid.ToString();
        string loserId = loser.Client!.Guid.ToString();

        int winnerCarRating = GetCarRating(winner.Model);
        int loserCarRating = GetCarRating(loser.Model);

        var (winnerElo, winnerRacesCompleted) = _plugin.GetPlayerStats(winnerId);
        var (loserElo, loserRacesCompleted) = _plugin.GetPlayerStats(loserId);

        int newWinnerElo = CalculateElo(winnerElo, loserElo, winnerCarRating, loserCarRating, true, winnerRacesCompleted);
        int newLoserElo = CalculateElo(loserElo, winnerElo, loserCarRating, winnerCarRating, false, loserRacesCompleted);

        // Update elo in the database.
        UpdatePlayerElo(winnerId, newWinnerElo);
        UpdatePlayerElo(loserId, newLoserElo);

        // Send new elo the clients.
        winner.Client!.SendPacket(new EloPacket { Elo = newWinnerElo });
        loser.Client!.SendPacket(new EloPacket { Elo = newLoserElo });
    }

    private int CalculateElo(int playerElo, int opponentElo, int playerCarRating, int opponentCarRating, bool hasWon, int racesCompleted)
    {
        // Calculate car performance difference factor
        double carAdvantage = (playerCarRating - opponentCarRating) / 100;

        // Adjust effective ratings based on car performance.
        double effectivePlayerElo = playerElo - carAdvantage * 100;

        // Calculate expected outcome (standard ELO formula)
        double expectedResult = 1.0 / (1.0 + Math.Pow(10.0, (opponentElo - effectivePlayerElo) / 400.0));

        int maxGain = _configuration.MaxEloGain;
        if (racesCompleted < _configuration.ProvisionalRaces)
        {
            maxGain = _configuration.MaxEloGainProvisional;
        }

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

        int newElo = playerElo + adjustedEloChange;

        // Ensure Elo rating never goes below 0
        return Math.Max(0, newElo);
    }

    private void UpdatePlayerElo(string playerId, int newElo)
    {
        using var connection = new SqliteConnection($"Data Source={_plugin.dbPath}");
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
    }

    private int GetCarRating(string carModel)
    {
        // Check if the rating is in the cfg file
        int performance = 500; // Default value
        if (_configuration.CarPerformanceRatings.TryGetValue(carModel, out int carPerformance))
        {
            performance = carPerformance;
        }
        return performance;
    }

    public void UpdateStandings(EntryCar winner, int round)
    {
        int index = round - 1;

        // Update the standings arrays
        if (winner == Challenger)
        {
            challengerStandings[index] = (int)RaceResult.Win;
            challengedStandings[index] = (int)RaceResult.Loss;
        }
        else
        {
            challengerStandings[index] = (int)RaceResult.Loss;
            challengedStandings[index] = (int)RaceResult.Win;
        }

        // Now update client side.
        SendStandings();
    }

    private void SendStandings(bool isHudOn = true)
    {
        Challenger.Client!.SendPacket(new StandingPacket { Result1 = challengerStandings[0], Result2 = challengerStandings[1], Result3 = challengerStandings[2], IsHudOn = isHudOn });
        Challenged.Client!.SendPacket(new StandingPacket { Result1 = challengedStandings[0], Result2 = challengedStandings[1], Result3 = challengedStandings[2], IsHudOn = isHudOn });
        // Maybe these values can be passed as an array in the future.
        // But I am happy it actually works for now :)
    }
}
