using System.Diagnostics;
using System.Linq;
using AssettoServer.Server.Configuration.Extra;
using Namotion.Reflection;
using NJsonSchema;
using NJsonSchema.Generation;
using YamlDotNet.Serialization;

namespace AssettoServer.Server.Configuration;

public class ConfigurationJsonSchemaGenerator : JsonSchemaGenerator, ISchemaProcessor
{
    private static readonly ACExtraConfiguration DefaultConfiguration = new();

    public override void ApplyDataAnnotations(JsonSchema schema, JsonTypeDescription typeDescription)
    {
        var yamlMemberAttribute = typeDescription.ContextualType.GetContextAttribute<YamlMemberAttribute>(true);
        schema.Description = yamlMemberAttribute?.Description;
        
        if (typeDescription.Type != JsonObjectType.Object && typeDescription.ContextualType.Context is ContextualPropertyInfo info)
        {
            object? defaultValue = null;
            // TODO improve this
            if (info.MemberInfo.DeclaringType == typeof(ACExtraConfiguration))
            {
                defaultValue = info.GetValue(DefaultConfiguration);
            }
            else if (info.MemberInfo.DeclaringType == typeof(AiParams))
            {
                defaultValue = info.GetValue(DefaultConfiguration.AiParams);
            }
            
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

    public ConfigurationJsonSchemaGenerator(JsonSchemaGeneratorSettings settings) : base(settings)
    {
    }
}
