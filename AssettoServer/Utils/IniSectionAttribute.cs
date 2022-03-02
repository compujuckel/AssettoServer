using System;

namespace AssettoServer.Utils;

[AttributeUsage(AttributeTargets.Property)]
public class IniSectionAttribute : Attribute
{
    public readonly string Section;

    public IniSectionAttribute(string section)
    {
        Section = section;
    }
}
