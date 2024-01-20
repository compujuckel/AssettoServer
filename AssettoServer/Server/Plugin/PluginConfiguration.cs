using System.Collections.Generic;
using JetBrains.Annotations;

namespace AssettoServer.Server.Plugin;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class PluginConfiguration
{
    public List<string> ExportedAssemblies { get; init; } = [];
}
