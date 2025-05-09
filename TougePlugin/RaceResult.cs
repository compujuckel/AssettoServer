using AssettoServer.Server;

namespace TougePlugin;
public enum RaceOutcome
{
    Win,
    Tie,
    Disconnected
}

public class RaceResult
{
    public RaceOutcome Outcome { get; }
    public EntryCar? Winner { get; } // Only valid when Outcome == Win

    private RaceResult(RaceOutcome outcome, EntryCar? winner = null)
    {
        Outcome = outcome;
        Winner = winner;
    }

    public static RaceResult Tie() => new RaceResult(RaceOutcome.Tie);
    public static RaceResult Disconnected() => new RaceResult(RaceOutcome.Disconnected);
    public static RaceResult Win(EntryCar winner) => new RaceResult(RaceOutcome.Win, winner);
}
