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
}
