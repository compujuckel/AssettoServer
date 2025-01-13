using System;
using System.Linq;
using System.Reflection;

namespace AssettoServer.Utils;

internal static class PropertyInfoExtensions
{
    internal static bool IsInitOnly(this PropertyInfo property)
    {
        var setMethod = property.SetMethod;
        if (setMethod == null)
            return false;

        var isExternalInitType = typeof(System.Runtime.CompilerServices.IsExternalInit);
        return setMethod.ReturnParameter.GetRequiredCustomModifiers().Contains(isExternalInitType);
    }

    internal static bool SetValueFromString(this PropertyInfo property, object target, string value, bool percent = false, bool allowInit = false)
    {
        var propertyType = property.PropertyType;
        if (propertyType == typeof(string))
        {
            if (!property.CanWrite || (!allowInit && property.IsInitOnly()))
                throw new InvalidOperationException($"Property {property.Name} is readonly");
            
            property.SetValue(target, value);
            return true;
        }
        
        if (propertyType.IsPrimitive)
        {
            if (!property.CanWrite || (!allowInit && property.IsInitOnly()))
                throw new InvalidOperationException($"Property {property.Name} is readonly");
            
            if (propertyType == typeof(bool))
            {
                property.SetValue(target, value != "0");
            }
            else
            {
                object? parsedValue = propertyType.GetMethod("Parse", [typeof(string)])!.Invoke(null, [value]);

                if (percent && parsedValue != null)
                {
                    parsedValue = (float)parsedValue / 100.0f;
                }

                property.SetValue(target, parsedValue);
            }

            return true;
        }
        
        if (propertyType.IsEnum)
        {
            if (!property.CanWrite || (!allowInit && property.IsInitOnly()))
                throw new InvalidOperationException($"Property {property.Name} is readonly");
            
            property.SetValue(target, Enum.Parse(propertyType, value, true));
            return true;
        }

        return false;
    }
}
