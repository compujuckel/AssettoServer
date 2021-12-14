using System;
using System.Reflection;

namespace AssettoServer.Server.Plugin;

public class Plugin
{
    public string Name { get; init; }
    public Assembly Assembly { get; init; }
    public IAssettoServerPlugin Instance { get; init; }
    public Type ConfigurationType { get; init; }
}