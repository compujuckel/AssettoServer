using System;
using System.Collections.Generic;
using System.IO;
using AssettoServer.Server.Configuration;
using McMaster.NETCore.Plugins;
using Newtonsoft.Json.Serialization;
using Serilog;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace AssettoServer.Server.Plugin;

public class ACPluginLoader
{
    public Dictionary<string, AvailablePlugin> AvailablePlugins { get; } = new();
    public List<LoadedPlugin> LoadedPlugins { get; } = [];

    public ACPluginLoader(bool loadFromWorkdir)
    {
        if (loadFromWorkdir)
        {
            var dir = Path.Join(Directory.GetCurrentDirectory(), "plugins");
            if (Directory.Exists(dir))
            {
                ScanDirectory(dir);
            }
            else
            {
                Directory.CreateDirectory(dir);
            }
        }
        
        string pluginsDir = Path.Combine(AppContext.BaseDirectory, "plugins");
        ScanDirectory(pluginsDir);
    }

    private void ScanDirectory(string path)
    {
        foreach (string dir in Directory.GetDirectories(path))
        {
            string dirName = Path.GetFileName(dir);
            string pluginDll = Path.Combine(dir, $"{dirName}.dll");
            if (File.Exists(pluginDll) && !AvailablePlugins.ContainsKey(dirName))
            {
                Log.Verbose("Found plugin {PluginName}, {PluginPath}", dirName, pluginDll);

                var loader = PluginLoader.CreateFromAssemblyFile(pluginDll, config => { config.PreferSharedTypes = true; });

                PluginConfiguration config;
                var configPath = Path.Combine(dir, "configuration.json");
                if (File.Exists(configPath))
                {
                    using var stream = File.OpenRead(configPath);
                    config = JsonSerializer.Deserialize<PluginConfiguration>(stream)!;
                }
                else
                {
                    config = new PluginConfiguration();
                }
                
                AvailablePlugins.Add(dirName, new AvailablePlugin(config, loader, dir));
            }
        }
    }

    internal void LoadPlugins(List<string> plugins)
    {
        foreach (var pluginName in plugins)
        {
            if (!AvailablePlugins.TryGetValue(pluginName, out var plugin))
            {
                throw new ConfigurationException($"No plugin found with name {pluginName}");
            }
            
            plugin.LoadExportedAssemblies();
        }

        foreach (var pluginName in plugins)
        {
            LoadPlugin(pluginName);
        }
    }

    private void LoadPlugin(string name)
    {
        if (!AvailablePlugins.TryGetValue(name, out var plugin))
        {
            throw new ConfigurationException($"No plugin found with name {name}");
        }
        
        var assembly = plugin.Load();
        
        foreach (var type in assembly.GetTypes())
        {
            if (typeof(AssettoServerModule).IsAssignableFrom(type) && !type.IsAbstract)
            {
                AssettoServerModule instance = Activator.CreateInstance(type) as AssettoServerModule ?? throw new InvalidOperationException("Could not create plugin instance");

                Type? configType = null;
                Type? validatorType = null;
                var baseType = type.BaseType!;
                if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(AssettoServerModule<>))
                {
                    configType = baseType.GetGenericArguments()[0];

                    foreach (var iface in configType.GetInterfaces())
                    {
                        if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IValidateConfiguration<>))
                        {
                            validatorType = iface.GetGenericArguments()[0];
                        }
                    }
                }

                LoadedPlugins.Add(new LoadedPlugin
                {
                    Name = name,
                    Directory = plugin.Path,
                    Assembly = assembly,
                    Instance = instance,
                    ConfigurationType = configType,
                    ValidatorType = validatorType,
                    ConfigurationFileName = configType != null ? ConfigurationTypeToFilename(configType.Name) : null,
                    SchemaFileName = configType != null ? ConfigurationTypeToFilename(configType.Name, "schema.json") : null,
                    ReferenceConfigurationFileName = configType != null ? ConfigurationTypeToFilename(configType.Name, "reference.yml") : null,
                    ReferenceConfiguration = instance.ReferenceConfiguration
                });
            }
        }
        
        Log.Information("Loaded plugin {PluginName}", name);
    }

    private static string ConfigurationTypeToFilename(string type, string ending = "yml")
    {
        var strat = new SnakeCaseNamingStrategy();
        type = type.Replace("Configuration", "Cfg");
        return $"plugin_{strat.GetPropertyName(type, false)}.{ending}";
    }
}
