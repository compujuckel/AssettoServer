namespace TrafficAiPlugin.Shared;

public interface ITrafficAi
{
    public IEntryCarTrafficAi GetAiCarBySessionId(byte sessionId);
    public float GetLaneWidthMeters();
}
