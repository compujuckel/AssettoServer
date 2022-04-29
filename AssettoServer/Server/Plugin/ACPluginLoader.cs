using System;
using System.Collections.Generic;
using System.IO;
using Autofac;
using McMaster.NETCore.Plugins;
using Serilog;

namespace AssettoServer.Server.Plugin;

public class ACPluginLoader
{
    public Dictionary<string, PluginLoader> AvailablePlugins { get; } = new();
    public List<Plugin> LoadedPlugins { get; } = new();

    public ACPluginLoader()
    {
        string pluginsDir = Path.Combine(AppContext.BaseDirectory, "plugins");
        foreach (string dir in Directory.GetDirectories(pluginsDir))
        {
            string dirName = Path.GetFileName(dir);
            string pluginDll = Path.Combine(dir, dirName + ".dll");
            if (File.Exists(pluginDll))
            {
                Log.Verbose("Found plugin {PluginName}, {PluginPath}", dirName, pluginDll);

                var loader = PluginLoader.CreateFromAssemblyFile(
                    pluginDll,
                    config => config.PreferSharedTypes = true);
                AvailablePlugins.Add(dirName, loader);
            }
        }
    }

    public void LoadPlugin(string name, ContainerBuilder builder)
    {
        if (!AvailablePlugins.TryGetValue(name, out var loader))
        {
            throw new ArgumentException($"No plugin found with name {name}");
        }
        
        var assembly = loader.LoadDefaultAssembly();

        foreach (var type in assembly.GetTypes())
        {
            if (typeof(AssettoServerModule).IsAssignableFrom(type) && !type.IsAbstract)
            {
                AssettoServerModule instance = Activator.CreateInstance(type) as AssettoServerModule ?? throw new InvalidOperationException("Could not create plugin instance");

                Type? configType = null;
                var baseType = type.BaseType!;
                if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(AssettoServerModule<>))
                {
                    configType = baseType.GetGenericArguments()[0];
                    builder.RegisterType(configType).AsSelf();
                }

                LoadedPlugins.Add(new Plugin(name, assembly, instance, configType));
            }
        }
        
        Log.Information("Loaded plugin {PluginName}", name);
    }

    public void LoadConfiguration(object? configuration, ContainerBuilder builder)
    {
        if (configuration != null)
        {
            builder.RegisterInstance(configuration).AsSelf();
        }
    }
}