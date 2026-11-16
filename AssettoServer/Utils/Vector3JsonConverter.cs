using System;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AssettoServer.Utils;

public sealed class Vector3JsonConverter : JsonConverter<Vector3>
{
    public override Vector3 Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        return new Vector3(
            root.GetProperty(GetName(nameof(Vector3.X))).GetSingle(),
            root.GetProperty(GetName(nameof(Vector3.Y))).GetSingle(),
            root.GetProperty(GetName(nameof(Vector3.Z))).GetSingle()
        );
        
        string GetName(string name) => options.PropertyNamingPolicy?.ConvertName(name) ?? name;
    }

    public override void Write(
        Utf8JsonWriter writer,
        Vector3 value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WriteNumber(GetName(nameof(Vector3.X)), value.X);
        writer.WriteNumber(GetName(nameof(Vector3.Y)), value.Y);
        writer.WriteNumber(GetName(nameof(Vector3.Z)), value.Z);

        writer.WriteEndObject();
        return;

        string GetName(string name) => options.PropertyNamingPolicy?.ConvertName(name) ?? name;
    }
}
