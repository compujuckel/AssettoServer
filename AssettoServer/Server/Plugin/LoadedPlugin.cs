using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace AssettoServer.Server.Plugin;

public class LoadedPlugin
{
    public required string Name { get; init; }
    public required string Directory { get; init; }
    public required Assembly Assembly { get; init; }
    public required AssettoServerModule Instance { get; init; }
    [MemberNotNullWhen(true, nameof(ConfigurationFileName), nameof(SchemaFileName), nameof(ConfigurationType), nameof(ReferenceConfigurationFileName), nameof(ReferenceConfiguration))]
    public bool HasConfiguration => ConfigurationType != null;
    public Type? ConfigurationType { get; init; }
    public Type? ValidatorType { get; init; }
    public string? ConfigurationFileName { get; init; }
    public string? SchemaFileName { get; init; }
    public string? ReferenceConfigurationFileName { get; init; }
    public object? ReferenceConfiguration { get; init; }
}
