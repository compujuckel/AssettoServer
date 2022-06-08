using System.Runtime.Serialization;
using System.ServiceModel;
using ProtoBuf.Grpc;

namespace AssettoServer.Hub.Contracts;

[DataContract]
public class WhitelistSubscriptionResponse
{
    [DataMember(Order = 1)]
    public List<ulong> Entries { get; set; } = new();
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
    IAsyncEnumerable<WhitelistSubscriptionResponse> SubscribeAsync(CallContext context = default);

    [OperationContract]
    Task AddAsync(WhitelistAddRequest request, CallContext context = default);
}
