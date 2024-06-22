using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AssettoServer.Server.Configuration;
using IniParser.Model;
using Serilog;

namespace AssettoServer.Utils;

public static class IniDataExtensions
{
    public static void Deserialize(this IniData data, object target, Type type, string? section = null)
    {
        var properties = type.GetProperties().Where(prop => prop.IsDefined(typeof(IniFieldAttribute), false));

        foreach (var property in properties)
        {
            var iniFieldAttribute = (IniFieldAttribute)(property.GetCustomAttributes(typeof(IniFieldAttribute), false)[0]);

            string iniFieldValue;
            if (iniFieldAttribute.Section == null)
            {
                iniFieldValue = section == null ? data.Global[iniFieldAttribute.Key] : data[section][iniFieldAttribute.Key];
            }
            else
            {
                iniFieldValue = data[iniFieldAttribute.Section][iniFieldAttribute.Key];
            }

            if (!string.IsNullOrEmpty(iniFieldValue))
            {
                iniFieldValue = iniFieldValue.Split(" ;")[0].TrimEnd();
                
                Log.Verbose("{Section}.{Key}={Value}", iniFieldAttribute.Section ?? section, iniFieldAttribute.Key, iniFieldValue);
                
                try
                {
                    property.SetValueFromString(target, iniFieldValue, iniFieldAttribute.Percent, true);
                }
                catch (TargetInvocationException ex)
                {
                    if (!iniFieldAttribute.IgnoreParsingErrors)
                    {
                        throw new ConfigurationException($"Error setting property {iniFieldAttribute.Section ?? section}.{iniFieldAttribute.Key}. Make sure the value is in the correct format.", ex.InnerException);
                    }
                }
            }
        }

        var sectionProperties = type.GetProperties().Where(prop => prop.IsDefined(typeof(IniSectionAttribute), false));
        foreach (var property in sectionProperties)
        {
            var iniSectionAttribute = (IniSectionAttribute)(property.GetCustomAttributes(typeof(IniSectionAttribute), false)[0]);
            
            var propertyType = property.PropertyType;
            if (propertyType.IsGenericType)
            {
                var listEntryType = propertyType.GetGenericArguments()[0];
                var listType = typeof(List<>).MakeGenericType(listEntryType);
                if (propertyType.IsAssignableFrom(listType))
                {
                    IList list = (IList)Activator.CreateInstance(listType)!;

                    for (int i = 0;; i++)
                    {
                        string listSection = $"{iniSectionAttribute.Section}_{i}";
                        if (!data.Sections.ContainsSection(listSection))
                            break;

                        object listEntry = Activator.CreateInstance(listEntryType)!;
                        data.Deserialize(listEntry, listEntryType, listSection);
                        list.Add(listEntry);
                    }

                    property.SetValue(target, list);
                }
            } 
            
            if(!data.Sections.ContainsSection(iniSectionAttribute.Section))
                continue; // TODO throw if property is not nullable

            object instance = Convert.ChangeType(Activator.CreateInstance(propertyType)!, propertyType);
            data.Deserialize(instance, propertyType, iniSectionAttribute.Section);
            property.SetValue(target, instance);
        }
    }
    public static void Deserialize<T>(this IniData data, T target, string? section = null) where T : class, new()
    {
        data.Deserialize(target, typeof(T), section);
    }

    public static T DeserializeObject<T>(this IniData data) where T : class, new()
    {
        T ret = new T();
        data.Deserialize(ret);
        return ret;
    }
}
