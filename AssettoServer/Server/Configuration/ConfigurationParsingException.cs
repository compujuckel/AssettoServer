using System;

namespace AssettoServer.Server.Configuration;

public class ConfigurationParsingException : Exception
{
    public string? Path { get; }
    
    public ConfigurationParsingException()
    {
    }

    public ConfigurationParsingException(string? path, Exception? inner = null)
        : base($"Error parsing configuration file {path}", inner)
    {
        Path = path;
    }
}
