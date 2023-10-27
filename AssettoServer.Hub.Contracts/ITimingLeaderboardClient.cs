using System.Runtime.Serialization;
using System.ServiceModel;
using ProtoBuf.Grpc;

namespace AssettoServer.Hub.Contracts;

[DataContract]
public class CreateTimedStageRequest
{
    [DataMember(Order = 1)]
    public string Name { get; set; } = "";
    [DataMember(Order = 2)]
    public string TrackName { get; set; } = "";
}

[DataContract]
public class CreateTimingLeaderboardRequest
{
    [DataMember(Order = 1)]
    public string Name { get; set; } = "";
}

[DataContract]
public class RegisterTimingLapTimeRequest
{
    [DataMember(Order = 1)]
    public ulong Guid { get; set; }
    [DataMember(Order = 2)]
    public string Leaderboard { get; set; } = "";
    [DataMember(Order = 3)]
    public string Track { get; set; } = "";
    [DataMember(Order = 4)]
    public string TimedStage { get; set; } = "";
    [DataMember(Order = 5)]
    public int LapTime { get; set; }
    [DataMember(Order = 6)]
    public string Car { get; set; } = "";
    [DataMember(Order = 7)]
    public bool Valid { get; set; }
    [DataMember(Order = 8)]
    public int SecurityLevel { get; set; }
    [DataMember(Order = 9)]
    public int InputMethod { get; set; }
    [DataMember(Order = 10)]
    public int WeatherFxType { get; set; } = -1;
    [DataMember(Order = 11)]
    public float TrackGrip { get; set; }
    [DataMember(Order = 12)]
    public float TrackTemperature { get; set; }
    [DataMember(Order = 13)]
    public float AmbientTemperature { get; set; }
    [DataMember(Order = 14)]
    public string? Tyre { get; set; }
    [DataMember(Order = 15)]
    public string? GameTime { get; set; }
    [DataMember(Order = 16)]
    public List<long>? Sectors { get; set; }
    [DataMember(Order = 17)]
    public int Shifter { get; set; }
    [DataMember(Order = 18)]
    public byte[]? CarChecksum { get; set; }
}

[DataContract]
public class TimingLeaderboardRequest
{
    [DataMember(Order = 1)]
    public string Leaderboard { get; set; } = "";
    [DataMember(Order = 2)]
    public string Track { get; set; } = "";
    [DataMember(Order = 3)]
    public string TimedStage { get; set; } = "";
    [DataMember(Order = 4)]
    public string? CarModel { get; set; }
}

[DataContract]
public class TimingLeaderboardEntry
{
    [DataMember(Order = 1)]
    public required string Name { get; set; }
    [DataMember(Order = 2)]
    public int LapTime { get; set; }
    [DataMember(Order = 3)]
    public required string CarModel { get; set; }
}

[DataContract]
public class TimingLeaderboardResponse
{
    [DataMember(Order = 1)] 
    public IEnumerable<TimingLeaderboardEntry> Entries { get; set; } = new List<TimingLeaderboardEntry>();
}

[DataContract]
public class TimingPersonalBestRequest
{
    [DataMember(Order = 1)]
    public string Leaderboard { get; set; } = "";
    [DataMember(Order = 2)]
    public string Track { get; set; } = "";
    [DataMember(Order = 3)]
    public string TimedStage { get; set; } = "";
    [DataMember(Order = 4)]
    public ulong Guid { get; set; }
    [DataMember(Order = 5)]
    public string? CarModel { get; set; }
}

[DataContract]
public class TimingPersonalBestResponse
{
    [DataMember(Order = 1)]
    public int? LapTime { get; set; }
    [DataMember(Order = 2)]
    public int? Rank { get; set; }
    [DataMember(Order = 3)]
    public required string CarModel { get; set; }
}

[ServiceContract]
public interface ITimingLeaderboardClient
{
    [OperationContract]
    public Task CreateTimedStage(CreateTimedStageRequest request, CallContext context = default);

    [OperationContract]
    public Task CreateLeaderboard(CreateTimingLeaderboardRequest request, CallContext context = default);

    [OperationContract]
    public Task RegisterLapTime(RegisterTimingLapTimeRequest request, CallContext context = default);
    
    [OperationContract]
    public Task<TimingLeaderboardResponse> GetLeaderboard(TimingLeaderboardRequest request, CallContext context = default);

    [OperationContract]
    public Task<TimingPersonalBestResponse> GetPersonalBest(TimingPersonalBestRequest request, CallContext context = default);
}
