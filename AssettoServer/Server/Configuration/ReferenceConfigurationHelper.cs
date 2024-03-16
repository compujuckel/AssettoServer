using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.ObjectGraphVisitors;

namespace AssettoServer.Server.Configuration;

public static class ReferenceConfigurationHelper
{
    private const string BaseFolder = "cfg/reference";
    
    public static void WriteReferenceConfiguration(string filename, string schemaPath, object config, string name)
    {
        Directory.CreateDirectory(BaseFolder);
        
        var path = Path.Join(BaseFolder, filename);
        
        // TODO this can be removed in future versions. Some v0.0.55 prerelease builds made these files readonly, so we undo that first for compatibility
        if (File.Exists(path))
        {
            var info = new FileInfo(path);
            info.IsReadOnly = false;
        }

        using var writer = File.CreateText(path);
        ConfigurationSchemaGenerator.WriteModeLine(writer, BaseFolder, schemaPath);
        writer.WriteLine($"# {name} Reference Configuration");
        writer.WriteLine("# This file serves as an overview of all possible options with their default values.");
        writer.WriteLine();

        var builder = new SerializerBuilder()
            .WithoutEmissionPhaseObjectGraphVisitor<DefaultValuesObjectGraphVisitor>()
            .Build();
            
        builder.Serialize(writer, config);
    }
}
