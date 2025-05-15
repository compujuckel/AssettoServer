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
    public EntryCar? ResultCar { get; }

    private RaceResult(RaceOutcome outcome, EntryCar? resultCar = null)
    {
        Outcome = outcome;
        ResultCar = resultCar;
    }

    public static RaceResult Tie() => new RaceResult(RaceOutcome.Tie);
    public static RaceResult Disconnected(EntryCar remainingPlayer) => new RaceResult(RaceOutcome.Disconnected, remainingPlayer);
    public static RaceResult Win(EntryCar winner) => new RaceResult(RaceOutcome.Win, winner);
}
