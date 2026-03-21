using System.Collections.Generic;
using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace AssettoServer.Server.Configuration.Extra;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class UserGroupCommandPermissions
{
    [YamlMember(Description = "Name of the user group")]
    public required string UserGroup { get; set; }
    [YamlMember(Description = "List of commands without slash prefix")]
    public required List<string> Commands { get; set; }
}
