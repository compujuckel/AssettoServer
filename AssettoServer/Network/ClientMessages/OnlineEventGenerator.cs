using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Text;
using AssettoServer.Server;
using AssettoServer.Shared.Network.Packets;
using AssettoServer.Shared.Network.Packets.Shared;
using AssettoServer.Shared.Utils;
using AssettoServer.Vendor.CSPXxHash3;
using Serilog;
using Sigil;

namespace AssettoServer.Network.ClientMessages;

internal static class OnlineEventGenerator
{
    private static readonly Dictionary<Type, string> TypeMapping = new()
    {
        { typeof(bool), "bool" },
        { typeof(byte), "uint8_t" },
        { typeof(sbyte), "char" },
        { typeof(ushort), "uint16_t" },
        { typeof(short), "int16_t" },
        { typeof(uint), "uint32_t" },
        { typeof(int), "int" },
        { typeof(ulong), "uint64_t" },
        { typeof(long), "int64_t" },
        { typeof(float), "float" },
        { typeof(double), "double" },
        { typeof(Vector2), "vec2" },
        { typeof(Vector3), "vec3" },
        { typeof(Vector4), "vec4" },
        { typeof(Color), "rgbm" },
    };

    private static uint GenerateKey(string definition)
    {
        var stream = new MemoryStream();
        stream.Write("server_script"u8);
        
        foreach (var chr in Encoding.UTF8.GetBytes(definition))
        {
            if (!" \f\n\r\t\v"u8.Contains(chr))
            {
                stream.WriteByte(chr);
            }
        }

        var hash = CSPXxHash3.Hash64(stream.ToArray());
        return (uint)hash ^ (uint)(hash >> 32);
    }

    // This code is translated from Lua: https://github.com/ac-custom-shaders-patch/acc-lua-sdk/blob/ca9530fbb5c81d0c23c4c1ba7a8f198870d2b2a3/common/ac_struct_item.lua#L188
    private static List<OnlineEventFieldInfo> ReorderFields(List<OnlineEventFieldInfo?> fields)
    {
        var reordered = new List<OnlineEventFieldInfo>();
        int i = 0;
        int count = fields.Count;
        int pos = 0;
        while (i < count)
        {
            if (fields[i] != null)
            {
                var v = fields[i]!;
                int l = 8 - pos % 8;
                if (v.Size > 1 && v.Size <= 8 && l < v.Size)
                {
                    for (int j = i + 1; j < count; j++)
                    {
                        if (fields[j] != null && fields[j]!.Size > 0 && fields[j]!.Size <= l)
                        {
                            v = fields[j];
                            i--;
                            fields[j] = null;
                            break;
                        }
                    }
                }

                reordered.Add(v!);
                pos += v!.Size;
            }

            i++;
        }

        return reordered;
    }

    private static bool IsArrayLikeType(Type type)
    {
        return type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(Memory<>) || type.GetGenericTypeDefinition() == typeof(ArraySegment<>));
    }

    internal static OnlineEventInfo ParseClientMessage(Type messageType)
    {
        var mainAttr = messageType.GetCustomAttribute<OnlineEventAttribute>() ?? new OnlineEventAttribute();
        var key = mainAttr.Key;

        var ordered = new List<OnlineEventFieldInfo?>();
        
        foreach (var field in messageType.GetFields())
        {
            var attr = field.GetCustomAttribute<OnlineEventFieldAttribute>();
            if (attr == null) continue;

            var type = field.FieldType;
            if ((type.IsArray || IsArrayLikeType(type) || type == typeof(string)) && attr.Size <= 0)
            {
                throw new InvalidOperationException($"No size specified for client message field {messageType.Name}.{field.Name}");
            }

            if (type.IsArray)
            {
                type = type.GetElementType()!;
            }
            else if (IsArrayLikeType(type))
            {
                type = type.GetGenericArguments()[0];
            }
            else if (type == typeof(string))
            {
                type = typeof(sbyte);
            }
            
            if (type.IsEnum)
            {
                type = Enum.GetUnderlyingType(type);
            }
 
            if (!TypeMapping.TryGetValue(type, out var defType))
            {
                throw new InvalidOperationException($"Unsupported type {type.Name} for client message field {messageType.Name}.{field.Name}");
            }

            int size;
            if (field.FieldType == typeof(string))
                size = -attr.Size;
            else if (field.FieldType == typeof(Color))
                size = 16;
            else
                size = MarshalUtils.SizeOf(type);

            ordered.Add(new OnlineEventFieldInfo
            {
                Name = attr.Name ?? field.Name,
                Type = field.FieldType,
                DefType = defType,
                Size = size,
                Array = attr.Size > 0 ? attr.Size : null,
                Field = field
            });
        }
        
        ordered.Sort((a, b) => a!.Size != b!.Size
            ? b.Size.CompareTo(a.Size)
            : string.Compare(a.Name, b.Name, StringComparison.Ordinal));

        var reordered = ReorderFields(ordered);
        var structure = GenerateStructure(key, reordered);
        
        var ret = new OnlineEventInfo
        {
            Key = key,
            Udp = mainAttr.Udp,
            Fields = reordered,
            Structure = structure,
            PacketType = GenerateKey(structure)
        };

        if (Program.IsDebugBuild)
        {
            Log.Debug("Parsed client message for {Class}, Type {Type:X}, Structure {Structure}",
                messageType.Name, ret.PacketType, ret.Structure);
        }

        return ret;
    }

    internal static OnlineEvent<TMessage>.FromReaderDelegate GenerateReaderMethod<TMessage>(OnlineEventInfo message)
        where TMessage : OnlineEvent<TMessage>, new()
    {
        var emitter = Emit<OnlineEvent<TMessage>.FromReaderDelegate>.NewDynamicMethod($"{typeof(TMessage).Name}.FromReader");
        var readMethod = typeof(PacketReader).GetMethod(nameof(PacketReader.Read))!;

        foreach (var field in message.Fields)
        {
            emitter.LoadArgument(0);
            emitter.LoadArgumentAddress(1);

            if (field.Type == typeof(string) && field.Array.HasValue)
            {
                var encoding = typeof(Encoding).GetProperty(nameof(Encoding.UTF8), 
                    BindingFlags.Public | BindingFlags.Static)!.GetMethod;
                emitter.Call(encoding);
                emitter.LoadConstant(field.Array.Value);
                emitter.Call(typeof(PacketReader).GetMethod(nameof(PacketReader.ReadStringFixed))!);
            }
            else if (field.Array.HasValue && (field.Type.IsArray || IsArrayLikeType(field.Type)))
            {
                var elementType = field.Type.IsArray ? field.Type.GetElementType()! : field.Type.GetGenericArguments()[0];
                
                emitter.LoadConstant(field.Array.Value);
                emitter.Call(typeof(PacketReader).GetMethod(nameof(PacketReader.ReadArrayFixed))!.MakeGenericMethod(elementType));
                var spanType = typeof(Span<>).MakeGenericType(elementType);
                using var loc = emitter.DeclareLocal(spanType);
                emitter.StoreLocal(loc);
                emitter.LoadLocalAddress(loc);
                emitter.Call(spanType.GetMethod("ToArray"));

                if (IsArrayLikeType(field.Type))
                {
                    var opImplicit = field.Type.GetMethod("op_Implicit", 
                        BindingFlags.Public | BindingFlags.Static, [elementType.MakeArrayType()])!;
                    emitter.Call(opImplicit);
                }
            }
            else if (field.Type == typeof(Color))
            {
                emitter.Call(typeof(PacketReader).GetMethod(nameof(PacketReader.ReadRgbmAsColor))!);
            }
            else if (field.Type.IsValueType)
            {
                emitter.Call(readMethod.MakeGenericMethod(field.Type));
            }
            else
            {
                throw new InvalidOperationException($"Cannot generate code for client message field {field.Name}");
            }
            
            emitter.StoreField(field.Field);
        }

        emitter.Return();
        return emitter.CreateDelegate();
    }

    internal static OnlineEvent<TMessage>.ToWriterDelegate GenerateWriterMethod<TMessage>(OnlineEventInfo message)
        where TMessage : OnlineEvent<TMessage>, new()
    {
        var emitter = Emit<OnlineEvent<TMessage>.ToWriterDelegate>.NewDynamicMethod($"{nameof(TMessage)}.ToWriter");
        var writeMethod = typeof(PacketWriter).GetMethod(nameof(PacketWriter.Write))!;

        emitter.LoadArgument(1);
        emitter.LoadConstant((int)ACServerProtocol.Extended);
        var writeByteMethod = writeMethod.MakeGenericMethod(typeof(byte));
        emitter.Call(writeByteMethod);

        emitter.LoadArgument(1);
        emitter.LoadConstant(message.Udp ? (int)CSPMessageTypeUdp.ClientMessage : (int)CSPMessageTypeTcp.ClientMessage);
        emitter.Call(writeByteMethod);
        
        emitter.LoadArgument(1);
        var sessionIdField = typeof(TMessage).GetField(nameof(OnlineEvent<TMessage>.SessionId));
        emitter.LoadArgument(0);
        emitter.LoadField(sessionIdField);
        emitter.Call(writeByteMethod);
        
        emitter.LoadArgument(1);
        emitter.LoadConstant((ushort)CSPClientMessageType.LuaMessage);
        var writeUshortMethod = writeMethod.MakeGenericMethod(typeof(ushort));
        emitter.Call(writeUshortMethod);
        
        emitter.LoadArgument(1);
        var packetTypeField = typeof(TMessage).GetField(nameof(OnlineEvent<TMessage>.PacketType), 
            BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        emitter.LoadField(packetTypeField);
        var writeUintMethod = writeMethod.MakeGenericMethod(typeof(uint));
        emitter.Call(writeUintMethod);

        for (var i = 0; i < message.Fields.Count; i++)
        {
            var field = message.Fields[i];
            
            emitter.LoadArgument(1);
            emitter.LoadArgument(0);
            
            if (field.Array.HasValue && field.Type == typeof(string))
            {
                emitter.LoadField(field.Field);
                var encoding = typeof(Encoding).GetProperty(nameof(Encoding.UTF8),
                    BindingFlags.Public | BindingFlags.Static)!.GetMethod;
                emitter.Call(encoding);
                emitter.LoadConstant(field.Array.Value);
                emitter.LoadConstant(i < message.Fields.Count - 1); // padding
                emitter.Call(typeof(PacketWriter).GetMethod(nameof(PacketWriter.WriteStringFixed))!);
            }
            else if (field.Array.HasValue && field.Type.IsArray)
            {
                emitter.LoadField(field.Field);
                var elementType = field.Type.GetElementType()!;
                var rosType = typeof(ReadOnlySpan<>).MakeGenericType(elementType);
                var opImplicit = rosType.GetMethod("op_Implicit", BindingFlags.Public | BindingFlags.Static, [field.Type])!;
                emitter.Call(opImplicit);
                emitter.LoadConstant(field.Array.Value);
                emitter.LoadConstant(i < message.Fields.Count - 1); // padding
                emitter.Call(typeof(PacketWriter).GetMethod(nameof(PacketWriter.WriteArrayFixed))!.MakeGenericMethod(elementType));
            }
            else if (field.Array.HasValue && field.Type.IsGenericType && field.Type.GetGenericTypeDefinition() == typeof(ArraySegment<>))
            {
                emitter.LoadField(field.Field);
                var elementType = field.Type.GetGenericArguments()[0];
                var rosType = typeof(ReadOnlySpan<>).MakeGenericType(elementType);
                var opImplicit = rosType.GetMethod("op_Implicit", BindingFlags.Public | BindingFlags.Static, [field.Type])!;
                emitter.Call(opImplicit);
                emitter.LoadConstant(field.Array.Value);
                emitter.LoadConstant(i < message.Fields.Count - 1); // padding
                emitter.Call(typeof(PacketWriter).GetMethod(nameof(PacketWriter.WriteArrayFixed))!.MakeGenericMethod(elementType));
            }
            else if (field.Array.HasValue && field.Type.IsGenericType && field.Type.GetGenericTypeDefinition() == typeof(Memory<>))
            {
                emitter.LoadFieldAddress(field.Field);
                var elementType = field.Type.GetGenericArguments()[0];
                var getSpan = field.Type.GetMethod("get_Span");
                emitter.Call(getSpan);
                var spanType = typeof(Span<>).MakeGenericType(elementType);
                var opImplicit = spanType.GetMethod("op_Implicit", BindingFlags.Public | BindingFlags.Static, [spanType])!;
                emitter.Call(opImplicit);
                emitter.LoadConstant(field.Array.Value);
                emitter.LoadConstant(i < message.Fields.Count - 1); // padding
                emitter.Call(typeof(PacketWriter).GetMethod(nameof(PacketWriter.WriteArrayFixed))!.MakeGenericMethod(elementType));
            }
            else if (field.Type == typeof(Color))
            {
                emitter.LoadField(field.Field);
                emitter.Call(typeof(PacketWriter).GetMethod(nameof(PacketWriter.WriteColorAsRgbm))!);
            }
            else if (field.Type.IsValueType)
            {
                emitter.LoadField(field.Field);
                emitter.Call(writeMethod.MakeGenericMethod(field.Type));
            }
            else
            {
                throw new InvalidOperationException($"Cannot generate code for client message field {field.Name}");
            }
        }

        emitter.Return();
        return emitter.CreateDelegate();
    }

    internal static string GenerateStructure(string? key, List<OnlineEventFieldInfo> fields)
    {
        var sb = new StringBuilder();
        foreach (var field in fields)
        {
            sb.Append($"{field.DefType} {field.Name}");
            if (field.Array.HasValue)
            {
                sb.Append($"[{field.Array.Value}]");
            }

            sb.Append(';');
        }

        if (key != null)
        {
            sb.Append($"//{key}");
        }

        return sb.ToString();
    }
}
