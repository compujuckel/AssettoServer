using AssettoServer.Server.Configuration;
using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace LogSessionPlugin;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class CCLogSessionConfiguration : IValidateConfiguration<LogSessionConfigurationValidator>
{
    [YamlMember(Description = "The unique ID that will be sent as part of the API POST request")]
    public int ServerId { get; init; }
    [YamlMember(Description = "Path to Crt for mTLS")]
    public string? CrtPath { get; init; }
    [YamlMember(Description = "Path to Key for mTLS")]
    public string? KeyPath { get; init; }

    [YamlMember(Description = "Url that will be POSTed to when players leave")]
    public string ApiUrlPlayerDisconnect { get; init; } = "";

    [YamlMember(Description = "Url that will be POSTed to when the session ends")]
    public string ApiUrlSessionEnd { get; init; } = "";

    [YamlMember(Description = "How often disconnected players should be synced")]
    public int SendDisconnectedFrequencyMinutes { get; init; } = 15;
    
}
