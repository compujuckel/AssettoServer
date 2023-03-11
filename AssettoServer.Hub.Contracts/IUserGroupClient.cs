using System.Runtime.Serialization;
using System.ServiceModel;
using ProtoBuf.Grpc;

namespace AssettoServer.Hub.Contracts;

[DataContract]
public class UserGroupSubscriptionResponse
{
    [DataMember(Order = 1)]
    public IReadOnlyList<ulong> Entries { get; set; } = new List<ulong>();
    [DataMember(Order = 2)]
    public int Version { get; set; }
}

[DataContract]
public class UserGroupSubscriptionRequest
{
    [DataMember(Order = 1)]
    public string Name { get; set; } = "";
    [DataMember(Order = 2)]
    public int Version { get; set; }
}

[DataContract]
public class UserGroupAddRequest
{
    [DataMember(Order = 1)]
    public string UserGroup { get; set; } = "";
    [DataMember(Order = 2)]
    public ulong Guid { get; set; }
}

[DataContract]
public class UserGroupAddResponse
{
    [DataMember(Order = 1)]
    public bool Success { get; set; }
}

[ServiceContract]
public interface IUserGroupClient
{
    [OperationContract]
    IAsyncEnumerable<UserGroupSubscriptionResponse> SubscribeAsync(UserGroupSubscriptionRequest request, CallContext context = default);

    [OperationContract]
    Task<UserGroupAddResponse> AddAsync(UserGroupAddRequest request, CallContext context = default);
}
