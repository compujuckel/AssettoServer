using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using AssettoServer.Server.Configuration.Extra;
using Namotion.Reflection;
using NJsonSchema;
using NJsonSchema.Generation;
using NJsonSchema.Generation.TypeMappers;
using YamlDotNet.Serialization;

namespace AssettoServer.Server.Configuration;

internal class ConfigurationSchemaGenerator : JsonSchemaGenerator, ISchemaProcessor
{
    private const string SchemaBasePath = "cfg/schemas";

    private ConfigurationSchemaGenerator(JsonSchemaGeneratorSettings settings) : base(settings)
    {
    }

    public static string WritePluginConfigurationSchema(Type type)
    {
        return WriteSchema(type, ACServerConfiguration.PluginConfigurationTypeToFilename(type.Name, "schema.json"));
    }

    public static string WriteExtraCfgSchema() => WriteSchema(typeof(ACExtraConfiguration), "extra_cfg.schema.json");

    private static string WriteSchema(Type type, string filename)
    {
        Directory.CreateDirectory(SchemaBasePath);
        
        var schema = GenerateSchema(type);
        var path = Path.Join(SchemaBasePath, filename);
        File.WriteAllText(path, schema);
        return path;
    }

    private static string GenerateSchema(Type type)
    {
        var settings = new SystemTextJsonSchemaGeneratorSettings
        {
            FlattenInheritanceHierarchy = true,
            SerializerOptions = new JsonSerializerOptions
            {
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            },
            TypeMappers = [
                new ObjectTypeMapper(typeof(Vector3), new JsonSchema
                {
                    Type = JsonObjectType.Object,
                    Properties =
                    {
                        { "X", new JsonSchemaProperty { Type = JsonObjectType.Number, Format = "float" }},
                        { "Y", new JsonSchemaProperty { Type = JsonObjectType.Number, Format = "float" }},
                        { "Z", new JsonSchemaProperty { Type = JsonObjectType.Number, Format = "float" }}
                    }
                })
            ]
        };

        var generator = new ConfigurationSchemaGenerator(settings);
        settings.SchemaProcessors.Add(generator);

        var schema = generator.Generate(type);
        return schema.ToJson();
    }
    
    public override void ApplyDataAnnotations(JsonSchema schema, JsonTypeDescription typeDescription)
    {
        var yamlMemberAttribute = typeDescription.ContextualType.GetContextAttribute<YamlMemberAttribute>(true);
        schema.Description = yamlMemberAttribute?.Description;
        
        if (typeDescription.Type != JsonObjectType.Object
            && typeDescription.ContextualType.Context is ContextualPropertyInfo info
            && info.CanRead
            && info.PropertyInfo.GetMethod!.GetParameters().Length == 0) // Special case for Vector3.Item[Int32]
        {
            var declaringObj = Activator.CreateInstance(info.MemberInfo.DeclaringType!)!;
            var defaultValue = info.GetValue(declaringObj);
            schema.Default = defaultValue;
        }
        
        base.ApplyDataAnnotations(schema, typeDescription);
    }

    public void Process(SchemaProcessorContext context)
    {
        if (context.ContextualType.GetContextAttribute<YamlIgnoreAttribute>(true) != null)
        {
            context.Schema.Title = "IGNORE";
        }

        var ignoredProperties = context.Schema.Properties.Where(p => p.Value.Title == "IGNORE").ToList();
        
        foreach (var property in ignoredProperties)
        {
            context.Schema.Properties.Remove(property.Key);
        }
    }

    public static void WriteModeLine(StreamWriter writer, string baseFolder, string schemaPath)
    {
        writer.WriteLine($"# yaml-language-server: $schema={Path.GetRelativePath(baseFolder, schemaPath)}");
        writer.WriteLine();
    }
}
