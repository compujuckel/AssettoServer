using System.Runtime.Serialization;
using System.ServiceModel;
using ProtoBuf.Grpc;

namespace AssettoServer.Hub.Contracts;

[DataContract]
public class RaceChallengeLeaderboardRequest
{
    [DataMember(Order = 1)]
    public required string LeaderboardName { get; set; }
    [DataMember(Order = 2)] 
    public int Skip { get; set; } = 0;
    [DataMember(Order = 3)] 
    public int Take { get; set; } = 10;
}

[DataContract]
public class RaceChallengeLeaderboardEntry
{
    [DataMember(Order = 1)]
    public required string Name { get; set; }
    [DataMember(Order = 2)]
    public int Rating { get; set; }
}

[DataContract]
public class RaceChallengeLeaderboardResponse
{
    [DataMember(Order = 1)] 
    public IEnumerable<RaceChallengeLeaderboardEntry> Entries { get; set; } = new List<RaceChallengeLeaderboardEntry>();
}

[DataContract]
public class GetRaceChallengeRatingRequest
{
    [DataMember(Order = 1)]
    public required string LeaderboardName { get; set; }
    [DataMember(Order = 2)]
    public ulong Guid { get; set; }
}

[DataContract]
public class GetRaceChallengeRatingResponse
{
    [DataMember(Order = 1)]
    public int Rating { get; set; }
    [DataMember(Order = 2)]
    public int Rank { get; set; }
}

[DataContract]
public class SetRaceChallengeRatingRequest
{
    [DataMember(Order = 1)]
    public required string LeaderboardName { get; set; }
    [DataMember(Order = 2)]
    public ulong Guid { get; set; }
    [DataMember(Order = 3)]
    public int Rating { get; set; }
}

[DataContract]
public class CreateRaceChallengeLeaderboardRequest
{
    [DataMember(Order = 1)]
    public required string Name { get; set; }
}

[ServiceContract]
public interface IRaceChallengeLeaderboardClient
{
    [OperationContract]
    Task<RaceChallengeLeaderboardResponse> GetLeaderboard(RaceChallengeLeaderboardRequest request, CallContext context = default);

    [OperationContract]
    Task<GetRaceChallengeRatingResponse> GetRating(GetRaceChallengeRatingRequest request, CallContext context = default);

    [OperationContract]
    Task SetRating(SetRaceChallengeRatingRequest request, CallContext context = default);

    [OperationContract]
    Task CreateLeaderboard(CreateRaceChallengeLeaderboardRequest request, CallContext context = default);
}
