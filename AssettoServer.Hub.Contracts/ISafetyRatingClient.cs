using System.Runtime.Serialization;
using System.ServiceModel;
using ProtoBuf.Grpc;

namespace AssettoServer.Hub.Contracts;

[DataContract]
public class UpdateRatingRequest
{
    [DataMember(Order = 1)]
    public ulong Guid { get; init; }
    [DataMember(Order = 2)]
    public double DistanceDriven { get; init; }
    [DataMember(Order = 3)]
    public double ContactPoints { get; init; }
}

[DataContract]
public class GetRatingRequest
{
    [DataMember(Order = 1)]
    public ulong Guid { get; init; }
}

[DataContract]
public class GetRatingResponse
{
    [DataMember(Order = 1)]
    public double Rating { get; init; }
    [DataMember(Order = 2)]
    public double DistanceDriven { get; init; }
    [DataMember(Order = 3)]
    public double ContactPoints { get; init; }
}

[ServiceContract]
public interface ISafetyRatingClient
{
    [OperationContract]
    public Task<GetRatingResponse> UpdateRating(UpdateRatingRequest request, CallContext context = default);

    [OperationContract]
    public Task<GetRatingResponse> GetRating(GetRatingRequest request, CallContext context = default);
}
