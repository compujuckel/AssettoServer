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
        {"comfymap", "Download comfy map! [https://www.racedepartment.com/downloads/comfy-map.52623/]"},
        {"discord", "Join the discord! [https://discord.gg/uXEXRcSkyz]"},
    };
}
