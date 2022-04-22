using System.Runtime.Serialization;
using System.ServiceModel;
using ProtoBuf.Grpc;

namespace AssettoServer.Hub.Contracts;

[DataContract]
public class LeaderboardRequest
{
    [DataMember(Order = 1)]
    public string LeaderboardName { get; set; } = null!;
    [DataMember(Order = 2)] 
    public int Skip { get; set; } = 0;
    [DataMember(Order = 3)] 
    public int Take { get; set; } = 10;
}

[DataContract]
public class LeaderboardEntry
{
    [DataMember(Order = 1)]
    public string Name { get; set; } = null!;
    [DataMember(Order = 2)]
    public int Rating { get; set; }
}

[DataContract]
public class LeaderboardResponse
{
    [DataMember(Order = 1)] 
    public IEnumerable<LeaderboardEntry> Entries { get; set; } = new List<LeaderboardEntry>();
}

[DataContract]
public class GetRatingRequest
{
    [DataMember(Order = 1)]
    public string LeaderboardName { get; set; } = null!;
    [DataMember(Order = 2)]
    public ulong Guid { get; set; }
}

[DataContract]
public class GetRatingResponse
{
    [DataMember(Order = 1)]
    public int Rating { get; set; }
    [DataMember(Order = 2)]
    public int Rank { get; set; }
}

[DataContract]
public class SetRatingRequest
{
    [DataMember(Order = 1)]
    public string LeaderboardName { get; set; } = null!;
    [DataMember(Order = 2)]
    public ulong Guid { get; set; }
    [DataMember(Order = 3)]
    public int Rating { get; set; }
}

[DataContract]
public class CreateLeaderboardRequest
{
    [DataMember(Order = 1)]
    public string Name { get; set; } = null!;
}

[ServiceContract]
public interface IRaceChallengeLeaderboardClient
{
    [OperationContract]
    Task<LeaderboardResponse> GetLeaderboard(LeaderboardRequest request, CallContext context = default);

    [OperationContract]
    Task<GetRatingResponse> GetRating(GetRatingRequest request, CallContext context = default);

    [OperationContract]
    Task SetRating(SetRatingRequest request, CallContext context = default);

    [OperationContract]
    Task CreateLeaderboard(CreateLeaderboardRequest request, CallContext context = default);
}
