using System;

namespace AssettoServer.Utils;

[AttributeUsage(AttributeTargets.Property)]
public class IniFieldAttribute : Attribute
{
    public readonly string? Section;
    public readonly string Key;
    public bool IgnoreParsingErrors = false;
    public bool Percent = false;

    public IniFieldAttribute(string key)
    {
        Key = key;
    }

    public IniFieldAttribute(string section, string key) : this(key)
    {
        Section = section;
    }
}
