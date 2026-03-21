using System;
using System.Collections.Generic;
using System.IO.Hashing;
using System.Runtime.InteropServices;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.ObjectGraphVisitors;

namespace AssettoServer.Utils;

public sealed class YamlNoRepeatingCommentsGraphVisitor(IObjectGraphVisitor<IEmitter> nextVisitor) : ChainedObjectGraphVisitor(nextVisitor)
{
    private readonly HashSet<ulong> _alreadySeenComments = [];
    private readonly Stack<Type> _currentType = new();

    private static ulong CalculateHash(string type, string key, string description)
    {
        var hash = new XxHash3();
        hash.Append(MemoryMarshal.Cast<char, byte>(type.AsSpan()));
        hash.Append(MemoryMarshal.Cast<char, byte>(key.AsSpan()));
        hash.Append(MemoryMarshal.Cast<char, byte>(description.AsSpan()));

        return hash.GetCurrentHashAsUInt64();
    }

    public override void VisitMappingStart(IObjectDescriptor mapping, Type keyType, Type valueType, IEmitter context, ObjectSerializer serializer)
    {
        _currentType.Push(mapping.Type);
        base.VisitMappingStart(mapping, keyType, valueType, context, serializer);
    }

    public override void VisitMappingEnd(IObjectDescriptor mapping, IEmitter context, ObjectSerializer serializer)
    {
        _currentType.Pop();
        base.VisitMappingEnd(mapping, context, serializer);
    }

    public override bool EnterMapping(IPropertyDescriptor key, IObjectDescriptor value, IEmitter context, ObjectSerializer serializer)
    {
        var yamlMember = key.GetCustomAttribute<YamlMemberAttribute>();
        if (yamlMember?.Description != null)
        {
            var hash = CalculateHash(_currentType.Peek().FullName ?? "", key.Name, yamlMember.Description);

            if (_alreadySeenComments.Add(hash))
            {
                context.Emit(new YamlDotNet.Core.Events.Comment(yamlMember.Description, false));
            }
        }

        return base.EnterMapping(key, value, context, serializer);
    }
}
