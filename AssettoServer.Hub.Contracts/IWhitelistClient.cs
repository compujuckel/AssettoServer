using System.Runtime.Serialization;
using System.ServiceModel;
using ProtoBuf.Grpc;

namespace AssettoServer.Hub.Contracts;

[DataContract]
public class WhitelistSubscriptionResponse
{
    [DataMember(Order = 1)]
    public IReadOnlyList<ulong> Entries { get; set; } = new List<ulong>();
    [DataMember(Order = 2)]
    public int Version { get; set; }
}

[DataContract]
public class WhitelistSubscriptionRequest
{
    [DataMember(Order = 1)]
    public int Version { get; set; }
}

[DataContract]
public class WhitelistAddRequest
{
    [DataMember(Order = 1)]
    public ulong Guid { get; set; }
}

[ServiceContract]
public interface IWhitelistClient
{
    [OperationContract]
    IAsyncEnumerable<WhitelistSubscriptionResponse> SubscribeAsync(WhitelistSubscriptionRequest request, CallContext context = default);

    [OperationContract]
    Task AddAsync(WhitelistAddRequest request, CallContext context = default);
}
