using System.Runtime.Serialization;
using System.ServiceModel;
using ProtoBuf.Grpc;

namespace AssettoServer.Hub.Contracts;

[DataContract]
public class RegisterOvertakeScoreRequest
{
    [DataMember(Order = 1)]
    public ulong Guid { get; set; }
    [DataMember(Order = 2)]
    public required string Leaderboard { get; set; }
    [DataMember(Order = 3)]
    public long Score { get; set; }
    [DataMember(Order = 4)]
    public long Duration { get; set; }
    [DataMember(Order = 5)]
    public required string Car { get; set; }
}

[DataContract]
public class CreateOvertakeLeaderboardRequest
{
    [DataMember(Order = 1)]
    public required string Name { get; set; }
}

[DataContract]
public class OvertakePersonalBestRequest
{
    [DataMember(Order = 1)]
    public required string Leaderboard { get; set; }
    [DataMember(Order = 2)]
    public ulong Guid { get; set; }
}

[DataContract]
public class OvertakePersonalBestResponse
{
    [DataMember(Order = 1)]
    public long Score { get; set; }
    [DataMember(Order = 2)]
    public int Rank { get; set; }
}

[DataContract]
public class RankForScoreRequest
{
    [DataMember(Order = 1)]
    public required string Leaderboard { get; init; }
    [DataMember(Order = 2)]
    public long Score { get; init; }
}

[DataContract]
public class RankForScoreResponse
{
    [DataMember(Order = 1)]
    public int Rank { get; init; }
}

[ServiceContract]
public interface IOvertakeLeaderboardClient
{
    [OperationContract]
    public Task CreateLeaderboard(CreateOvertakeLeaderboardRequest request, CallContext context = default);
    
    [OperationContract]
    public Task<OvertakePersonalBestResponse> RegisterScore(RegisterOvertakeScoreRequest request, CallContext context = default);
    
    [OperationContract]
    public Task<OvertakePersonalBestResponse> GetPersonalBest(OvertakePersonalBestRequest request, CallContext context = default);

    [OperationContract]
    public Task<RankForScoreResponse> GetRankForScore(RankForScoreRequest request, CallContext context = default);
}
