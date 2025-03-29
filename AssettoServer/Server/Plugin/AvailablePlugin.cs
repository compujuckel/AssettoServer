using System.Reflection;
using System.Runtime.Loader;
using McMaster.NETCore.Plugins;

namespace AssettoServer.Server.Plugin;

public class AvailablePlugin
{
    private readonly PluginConfiguration _configuration;
    private readonly PluginLoader _loader;
    public string Path { get; }

    public AvailablePlugin(PluginConfiguration configuration, PluginLoader loader, string path)
    {
        _configuration = configuration;
        _loader = loader;
        Path = path;
    }

    public Assembly Load() => _loader.LoadDefaultAssembly();

    public void LoadExportedAssemblies()
    {
        foreach (var assemblyName in _configuration.ExportedAssemblies)
        {
            var fileName = System.IO.Path.GetFileName(assemblyName);
            var fullPath = System.IO.Path.Combine(Path, fileName);
            AssemblyLoadContext.Default.LoadFromAssemblyPath(fullPath);
        }
    }
}
