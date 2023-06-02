using System.Runtime.Serialization;
using System.ServiceModel;
using ProtoBuf.Grpc;

namespace AssettoServer.Hub.Contracts;

[DataContract]
public class UserProfileRequest
{
    [DataMember(Order = 1)]
    public ulong Guid { get; init; }
}

[DataContract]
public class UserProfileResponse
{
    [DataMember(Order = 1)]
    public string DiscordName { get; init; } = "";
    [DataMember(Order = 2)]
    public string AvatarImageUrl { get; init; } = "";
    [DataMember(Order = 3)]
    public string AboutMe { get; init; } = "";
    [DataMember(Order = 4)]
    public List<UserRoleResponse> Roles { get; init; } = new();
    [DataMember(Order = 5)]
    public uint Color { get; init; }
    [DataMember(Order = 6)]
    public ulong SteamId { get; init; }
    [DataMember(Order = 7)]
    public ulong DiscordId { get; init; }
}

[DataContract]
public class UserRoleResponse
{
    [DataMember(Order = 1)]
    public uint Color { get; init; }
    [DataMember(Order = 2)]
    public required string Name { get; init; }
    [DataMember(Order = 3)]
    public string? IconUrl { get; init; }
}

[ServiceContract]
public interface ISocialClient
{
    [OperationContract]
    public Task<UserProfileResponse> GetProfile(UserProfileRequest request, CallContext context = default);
}
