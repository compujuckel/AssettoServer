using System;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AssettoServer.Utils;

public sealed class Vector3JsonConverter : JsonConverter<Vector3>
{
    public override Vector3 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        return new Vector3(
            root.GetProperty("X").GetSingle(),
            root.GetProperty("Y").GetSingle(),
            root.GetProperty("Z").GetSingle()
        );
    }

    public override void Write(Utf8JsonWriter writer, Vector3 value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WriteNumber("X", value.X);
        writer.WriteNumber("Y", value.Y);
        writer.WriteNumber("Z", value.Z);

        writer.WriteEndObject();
    }
}
