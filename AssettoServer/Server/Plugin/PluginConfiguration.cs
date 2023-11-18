using System.Collections.Generic;

namespace AssettoServer.Server.Plugin;

public class PluginConfiguration
{
    public List<string> ExportedAssemblies { get; init; } = [];
}
