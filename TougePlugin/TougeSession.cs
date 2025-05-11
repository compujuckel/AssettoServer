using AssettoServer.Server;
using TougePlugin.Packets;
using Serilog;

namespace TougePlugin;

public class TougeSession
{
    public EntryCar Challenger { get; }
    public EntryCar Challenged { get; }

    private int winCounter = 0;
    private readonly int[] challengerStandings = [(int)RaceResultCounter.Tbd, (int)RaceResultCounter.Tbd, (int)RaceResultCounter.Tbd];
    private readonly int[] challengedStandings = [(int)RaceResultCounter.Tbd, (int)RaceResultCounter.Tbd, (int)RaceResultCounter.Tbd];

    private const int coolDownTime = 10000; // Cooldown timer for inbetween races.

    private enum RaceResultCounter
    {
        Tbd = 0,
        Win = 1,
        Loss = 2,
    }

    public bool IsActive { get; private set; }

    private readonly EntryCarManager _entryCarManager;
    private readonly Touge _plugin;
    private readonly Race.Factory _raceFactory;
    private readonly TougeConfiguration _configuration;

    public delegate TougeSession Factory(EntryCar challenger, EntryCar challenged);

    public TougeSession(EntryCar challenger, EntryCar challenged, EntryCarManager entryCarManager, Touge plugin, Race.Factory raceFactory, TougeConfiguration configuration)
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
            // Turn on the hud
            SendStandings(true);
            RaceResult result = await FirstTwoRaceAsync();

            if (result.Outcome != RaceOutcome.Disconnected)
            {
                // If the result of the first two races is a tie, race until there is a winner.
                // Sudden death style.
                bool isChallengerLeading = true; // Challenger as leader at first.
                while (result.Outcome == RaceOutcome.Tie)
                {
                    // Swap the posistion of leader and follower.
                    EntryCar leader = isChallengerLeading ? Challenger : Challenged;
                    EntryCar follower = isChallengerLeading ? Challenged : Challenger;

                    Race race = _raceFactory(leader, follower);
                    await Task.Delay(coolDownTime); // Small cooldown time inbetween races.
                    result = await race.RaceAsync();

                    isChallengerLeading = !isChallengerLeading;
                }

                if (result.Outcome != RaceOutcome.Disconnected)
                {
                    UpdateStandings(result.Winner!, winCounter);
                    UpdateEloAsync(result.Winner!);
                    // In the current situation players can rage dc and deny the oponnent a win.
                    // Maybe elo should always be subtracted when leaving.
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while running touge session.");
        }
        finally
        {
            await FinishTougeSessionAsync();
        }
    }

    private async Task<RaceResult> FirstTwoRaceAsync()
    {
        // Run race 1.
        Race race1 = _raceFactory(Challenger, Challenged);
        RaceResult result1 = await race1.RaceAsync();

        // If both players are still connected. Run race 2.
        if (result1.Outcome != RaceOutcome.Disconnected)
        {
            if (result1.Outcome == RaceOutcome.Win)
            {
                UpdateStandings(result1.Winner!, winCounter);
                winCounter++;
            }

            // Always start second race.
            await Task.Delay(coolDownTime); // Small cooldown time inbetween races.
            Race race2 = _raceFactory(Challenged, Challenger);
            RaceResult result2 = await race2.RaceAsync();

            if (result2.Outcome != RaceOutcome.Disconnected)
            {
                if (result2.Outcome == RaceOutcome.Win)
                {
                    UpdateStandings(result2.Winner!, winCounter);
                    winCounter++;
                }

                // Both races are finished. Check what to return.
                if (IsTie(result1, result2))
                {
                    return RaceResult.Tie();
                }
                else
                {
                    // Its either 0-1 or 0-2.
                    return RaceResult.Win(result1.Winner!);
                }
            }
        }
        return RaceResult.Disconnected();
    }

    private bool IsTie(RaceResult r1, RaceResult r2)
    {
        bool bothAreWins = r1.Outcome == RaceOutcome.Win && r2.Outcome == RaceOutcome.Win;
        bool differentWinners = r1.Winner != r2.Winner;
        return winCounter == 0 || (bothAreWins && differentWinners);
    }

    private async Task FinishTougeSessionAsync()
    {
        _plugin.GetSession(Challenger).CurrentSession = null;
        _plugin.GetSession(Challenged).CurrentSession = null;

        string ChallengedName = Challenged.Client?.Name!;
        string ChallengerName = Challenger.Client?.Name!;

        _entryCarManager.BroadcastChat($"Race between {ChallengerName} and {ChallengedName} has concluded!");

        await Task.Delay(coolDownTime); // Small cooldown to shortly keep scoreboard up after session has ended.

        // Turn off and reset hud
        Array.Fill(challengerStandings, (int)RaceResultCounter.Tbd);
        Array.Fill(challengedStandings, (int)RaceResultCounter.Tbd);
        SendStandings(false);
    }

    private async void UpdateEloAsync(EntryCar? winner)
    {
        if (winner == null) return;
        var loser = (winner == Challenger) ? Challenged : Challenger;

        string winnerId = winner.Client!.Guid.ToString();
        string loserId = loser.Client!.Guid.ToString();

        int winnerCarRating = GetCarRating(winner.Model);
        int loserCarRating = GetCarRating(loser.Model);

        var (winnerElo, winnerRacesCompleted) = await _plugin.database.GetPlayerStatsAsync(winnerId);
        var (loserElo, loserRacesCompleted) = await _plugin.database.GetPlayerStatsAsync(loserId);

        int newWinnerElo = CalculateElo(winnerElo, loserElo, winnerCarRating, loserCarRating, true, winnerRacesCompleted);
        int newLoserElo = CalculateElo(loserElo, winnerElo, loserCarRating, winnerCarRating, false, loserRacesCompleted);

        // Update elo in the database.
        await _plugin.database.UpdatePlayerEloAsync(winnerId, newWinnerElo);
        await _plugin.database.UpdatePlayerEloAsync(loserId, newLoserElo);

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

    public void UpdateStandings(EntryCar winner, int scoreboardIndex)
    {
        // Update the standings arrays
        if (winner == Challenger)
        {
            challengerStandings[scoreboardIndex] = (int)RaceResultCounter.Win;
            challengedStandings[scoreboardIndex] = (int)RaceResultCounter.Loss;
        }
        else
        {
            challengerStandings[scoreboardIndex] = (int)RaceResultCounter.Loss;
            challengedStandings[scoreboardIndex] = (int)RaceResultCounter.Win;
        }

        // Now update client side.
        SendStandings();
    }

    private void SendStandings(bool isHudOn = true)
    {
        Challenger.Client!.SendPacket(new StandingPacket { Result1 = challengerStandings[0], Result2 = challengerStandings[1], Result3 = challengerStandings[2], IsHudOn = isHudOn });
        Challenged.Client!.SendPacket(new StandingPacket { Result1 = challengedStandings[0], Result2 = challengedStandings[1], Result3 = challengedStandings[2], IsHudOn = isHudOn });
    }
}
