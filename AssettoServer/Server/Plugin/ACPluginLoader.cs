using System;
using System.Collections.Generic;
using System.IO;
using McMaster.NETCore.Plugins;
using Serilog;

namespace AssettoServer.Server.Plugin;

public class ACPluginLoader
{
    public Dictionary<string, PluginLoader> AvailablePlugins { get; } = new();
    public List<Plugin> LoadedPlugins = new();

    public ACPluginLoader()
    {
        var loaders = new List<PluginLoader>();

        // create plugin loaders
        string pluginsDir = Path.Combine(AppContext.BaseDirectory, "plugins");
        foreach (string dir in Directory.GetDirectories(pluginsDir))
        {
            string dirName = Path.GetFileName(dir);
            string pluginDll = Path.Combine(dir, dirName + ".dll");
            if (File.Exists(pluginDll))
            {
                Log.Verbose("Found plugin {0}, {1}", dirName, pluginDll);

                var loader = PluginLoader.CreateFromAssemblyFile(
                    pluginDll,
                    config => config.PreferSharedTypes = true);
                AvailablePlugins.Add(dirName, loader);
            }
        }
    }

    public void LoadPlugin(string name)
    {
        if (!AvailablePlugins.TryGetValue(name, out var loader))
        {
            throw new ArgumentException($"No plugin found with name {name}");
        }
        
        var assembly = loader.LoadDefaultAssembly();

        foreach (var type in assembly.GetTypes())
        {
            if (typeof(IAssettoServerPlugin).IsAssignableFrom(type) && !type.IsAbstract)
            {
                IAssettoServerPlugin instance = (IAssettoServerPlugin)Activator.CreateInstance(type);

                Type configType = null;
                foreach (var inface in type.GetInterfaces())
                {
                    if (inface.IsGenericType && inface.GetGenericTypeDefinition() == typeof(IAssettoServerPlugin<>))
                    {
                        configType = inface.GetGenericArguments()[0];
                    }
                }
                
                LoadedPlugins.Add(new Plugin
                {
                    Name = name,
                    Assembly = assembly,
                    Instance = instance,
                    ConfigurationType = configType
                });
            }
        }
        
        Log.Information("Loaded plugin {0}", name);
    }

    public void LoadConfiguration(object configuration)
    {
        foreach (var plugin in LoadedPlugins)
        {
            if (plugin.ConfigurationType != null && plugin.ConfigurationType.IsInstanceOfType(configuration))
            {
                var genericType = typeof(IAssettoServerPlugin<>).MakeGenericType(new[] { plugin.ConfigurationType });
                var method = genericType.GetMethod("SetConfiguration");

                method.Invoke(plugin.Instance, new []{ configuration });
                break;
            }
        }
    }
}