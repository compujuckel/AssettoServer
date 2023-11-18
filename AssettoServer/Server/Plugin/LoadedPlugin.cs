using System;
using System.Reflection;

namespace AssettoServer.Server.Plugin;

public class LoadedPlugin
{
    public string Name { get; }
    public Assembly Assembly { get; }
    public AssettoServerModule Instance { get; }
    public Type? ConfigurationType { get; }
    public Type? ValidatorType { get; }

    public LoadedPlugin(string name, Assembly assembly, AssettoServerModule instance, Type? configurationType, Type? validatorType)
    {
        Name = name;
        Assembly = assembly;
        Instance = instance;
        ConfigurationType = configurationType;
        ValidatorType = validatorType;
    }
}
