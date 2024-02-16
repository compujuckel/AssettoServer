using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace AssettoServer.Server.Steam;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class AuthenticateUserTicketResponse
{
    [JsonPropertyName("response")]
    public required ResponseObj Response { get; init; }

    [UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
    public class ResponseObj
    {
        [JsonPropertyName("params")]
        public ParamsObj? Params { get; init; }
        [JsonPropertyName("error")]
        public ErrorObj? Error { get; init; }

        [UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
        public class ParamsObj
        {
            [JsonPropertyName("result")]
            public required string Result { get; init; }
            [JsonPropertyName("steamid")]
            public required string SteamId { get; init; }
            [JsonPropertyName("ownersteamid")]
            public required string OwnerSteamId { get; init; }
            [JsonPropertyName("vacbanned")]
            public bool VacBanned { get; init; }
            [JsonPropertyName("publisherbanned")]
            public bool PublisherBanned { get; init; }
        }
        
        [UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
        public class ErrorObj
        {
            [JsonPropertyName("errorcode")]
            public int ErrorCode { get; init; }
            [JsonPropertyName("errordesc")]
            public required string ErrorDesc { get; init; }
        }
    }
}
