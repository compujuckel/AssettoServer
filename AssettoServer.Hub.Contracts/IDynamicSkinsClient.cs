using System.Runtime.Serialization;
using System.ServiceModel;
using ProtoBuf.Grpc;

namespace AssettoServer.Hub.Contracts;

[DataContract]
public class ListSkinsRequest
{
    [DataMember(Order = 1)]
    public required string CarModel { get; init; }
    [DataMember(Order = 2)]
    public ulong Guid { get; init; }
}

[DataContract]
public class Skin
{
    [DataMember(Order = 1)]
    public required string Name { get; init; }
    [DataMember(Order = 2)]
    public required string SkinUrl { get; init; }
    [DataMember(Order = 3)]
    public required string IconUrl { get; init; }
}

[DataContract]
public class ListSkinsResponse
{
    [DataMember(Order = 1)]
    public required IDictionary<string, Skin> Skins { get; init; } = new Dictionary<string, Skin>();
}

[ServiceContract]
public interface IDynamicSkinsClient
{
    [OperationContract]
    Task<ListSkinsResponse> ListSkins(ListSkinsRequest request, CallContext context = default);
}
