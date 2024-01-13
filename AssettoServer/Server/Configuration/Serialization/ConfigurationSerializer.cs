using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AssettoServer.Utils;
using YamlDotNet.Serialization;

namespace AssettoServer.Server.Configuration.Serialization;

public class ConfigurationSerializer
{ 
    public ConfigurationProperty? ParseProperty(object? obj, PropertyInfo property)
    {
        string? description = null;
        string type = property.PropertyType.ToString();
        
        foreach (var attr in property.GetCustomAttributes(false))
        {
            if (attr is YamlMemberAttribute memberAttribute)
            {
                description = memberAttribute.Description ?? "";
            }
            else if (attr is YamlIgnoreAttribute)
            {
                return null;
            }
            else if (attr is IniFieldAttribute iniFieldAttribute)
            {
                if (iniFieldAttribute.Section != null)
                {
                    description = $"{iniFieldAttribute.Section}.{iniFieldAttribute.Key}";
                }
                else
                {
                    description = iniFieldAttribute.Key;
                }
            }
        }

        object? propertyValue = property.GetValue(obj);
        object? val = null;
        string? entryType = null;
        string[]? validValues = null;
        bool nullable = false;
        var underlying = Nullable.GetUnderlyingType(property.PropertyType);
        
        if (property.PropertyType.IsPrimitive || property.PropertyType == typeof(string))
        {
            val = propertyValue;
            type = property.PropertyType.ToString();
        }
        else if (underlying != null)
        {
            nullable = true;
            val = propertyValue;
            type = underlying.ToString();
        }
        else if (property.PropertyType.IsEnum)
        {
            val = property.PropertyType.GetEnumName(propertyValue!);
            type = "enum";
            validValues = property.PropertyType.GetEnumNames();
        }
        else if (propertyValue is IDictionary dictProperty)
        {
            var dictEntryType = property.PropertyType.GetGenericArguments()[1];
            type = "dict";

            if (dictEntryType.IsPrimitive || dictEntryType == typeof(string))
            {
                val = dictProperty;
                entryType = dictEntryType.ToString();
            }
            else
            {
                var dict = new Dictionary<object, object?>();
                foreach (DictionaryEntry entry in dictProperty)
                {
                    dict.Add(entry.Key, ParseSection(entry.Value));
                }

                val = dict;
                entryType = "object";
            }
        }
        else if (propertyValue is IList listProperty)
        {
            var listEntryType = property.PropertyType.GetGenericArguments()[0];
            if (listEntryType.IsPrimitive || listEntryType == typeof(string))
            {
                val = listProperty;
                entryType = listEntryType.ToString();
            }
            else
            {
                var list = new List<object?>();
                foreach (var item in listProperty)
                {
                    list.Add(ParseSection(item));
                }

                val = list;
                entryType = "object";
            }

            type = "list";
        }
        else if (!property.PropertyType.IsGenericType)
        {
            val = ParseSection(propertyValue);
            type = "object";
        }

        return new ConfigurationProperty
        {
            Name = property.Name,
            Value = val,
            ReadOnly = !property.CanWrite || property.IsInitOnly(),
            Description = description,
            Type = type,
            ValidValues = validValues,
            EntryType = entryType,
            Nullable = nullable
        };
    }

    public ConfigurationObject? ParseSection(object? obj)
    {
        if (obj == null) return null;

        var ret = new List<ConfigurationProperty>();
        
        foreach (var prop in obj.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            // Special case for Vector3 Item[]
            if (prop.GetMethod?.GetParameters().Length > 0) continue;
            
            var parsed = ParseProperty(obj, prop);
            if (parsed != null)
            {
                ret.Add(parsed);
            }
        }

        return new ConfigurationObject
        {
            Properties = ret
        };
    }
}
