using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using McMaster.NETCore.Plugins;

namespace AssettoServer.Server.Plugin;

public class AvailablePlugin
{
    private readonly PluginConfiguration _configuration;
    private readonly PluginLoader _loader;
    private readonly string _path;

    public AvailablePlugin(PluginConfiguration configuration, PluginLoader loader, string path)
    {
        _configuration = configuration;
        _loader = loader;
        _path = path;
    }

    public Assembly Load() => _loader.LoadDefaultAssembly();

    public void LoadExportedAssemblies()
    {
        foreach (var assemblyName in _configuration.ExportedAssemblies)
        {
            var fileName = Path.GetFileName(assemblyName);
            var fullPath = Path.Combine(_path, fileName);
            AssemblyLoadContext.Default.LoadFromAssemblyPath(fullPath);
        }
    }
}
