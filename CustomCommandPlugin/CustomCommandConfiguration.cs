using AssettoServer.Server.Configuration;
using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace CustomCommandPlugin;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class CustomCommandConfiguration
{
    [YamlMember(Description = "Configure your custom commands")]
    public Dictionary<string, string> Commands { get; init; } = new()
    {
        {"discord", "https://discord.gg/uXEXRcSkyz"}
    };
}
