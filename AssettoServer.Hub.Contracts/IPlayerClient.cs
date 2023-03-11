using System.Runtime.Serialization;
using System.ServiceModel;
using ProtoBuf.Grpc;

namespace AssettoServer.Hub.Contracts;

[DataContract]
public class OnPlayerConnectedRequest
{
    [DataMember(Order = 1)]
    public ulong Guid { get; set; }
    [DataMember(Order = 2)]
    public required string Name { get; set; }
}

[ServiceContract]
public interface IPlayerClient
{
    [OperationContract]
    Task OnPlayerConnectedAsync(OnPlayerConnectedRequest request, CallContext context = default);
}
