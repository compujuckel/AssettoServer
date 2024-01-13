using System.Collections.Generic;
using JetBrains.Annotations;

namespace AssettoServer.Server.Configuration.Extra;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class UserGroupCommandPermissions
{
    public required string UserGroup { get; set; }
    public required List<string> Commands { get; set; }
}
