namespace AssettoServer.Server.Configuration
{
    public class SessionConfiguration
    {
        public int Id { get; init; }
        public SessionType Type => (SessionType)Id + 1;
        public string Name { get; init; } = "";
        public int Time { get; init; }
        public int Laps { get; init; }
        public int WaitTime { get; init; }
        public bool IsOpen { get; init; }
        public bool IsTimedRace => Time > 0 && Laps == 0;
    }

    public enum SessionType : byte
    {
        Booking = 0,
        Practice,
        Qualifying,
        Race
    }
}
