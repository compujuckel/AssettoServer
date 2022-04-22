using System.Runtime.Serialization;
using System.ServiceModel;
using ProtoBuf.Grpc;

namespace AssettoServer.Hub.Contracts;

[DataContract]
public class UpdateNameRequest
{
    [DataMember(Order = 1)]
    public ulong Guid { get; set; }
    [DataMember(Order = 2)]
    public string Name { get; set; } = null!;
}

[ServiceContract]
public interface IPlayerClient
{
    [OperationContract]
    Task UpdateNameAsync(UpdateNameRequest request, CallContext context = default);
}
