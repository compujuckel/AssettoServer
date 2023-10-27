using System.Runtime.Serialization;
using System.ServiceModel;
using ProtoBuf.Grpc;

namespace AssettoServer.Hub.Contracts;

[DataContract]
public class OnStartupRequest
{
    [DataMember(Order = 1)] 
    public string Ping { get; set; } = "";
    [DataMember(Order = 2)]
    public string Track { get; set; } = "";
    [DataMember(Order = 3)]
    public string TrackConfig { get; set; } = "";
    [DataMember(Order = 4)]
    public IEnumerable<string> Cars { get; set; } = new List<string>();
}

[DataContract]
public class OnStartupResponse
{
    [DataMember(Order = 1)]
    public string Pong { get; set; } = "";
}

[DataContract]
public class NameUpdateRequest
{
    [DataMember(Order = 1)]
    public required string Id { get; set; }
    [DataMember(Order = 2)]
    public required string FriendlyName { get; set; }
}

[ServiceContract]
public interface IServerClient
{
    [OperationContract]
    public Task<OnStartupResponse> OnStartup(OnStartupRequest request, CallContext context = default);

    [OperationContract]
    public Task UpdateTrackName(NameUpdateRequest request, CallContext context = default);

    [OperationContract]
    public Task UpdateCarName(NameUpdateRequest request, CallContext context = default);
}
