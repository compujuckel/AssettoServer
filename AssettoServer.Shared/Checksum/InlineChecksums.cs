using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AssettoServer.Shared.Checksum;

[InlineArray(Length)]
[JsonConverter(typeof(Md5ChecksumJsonConverter))]
public struct Md5Checksum : IEquatable<Md5Checksum>
{
    public const int Length = 16;
    private byte _element0;

    public static Md5Checksum FromBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != Length)
            throw new ArgumentException($"MD5 checksums must contain exactly {Length} bytes", nameof(bytes));

        var checksum = new Md5Checksum();
        bytes.CopyTo(checksum);
        return checksum;
    }

    public readonly byte[] ToArray() => ((ReadOnlySpan<byte>)this).ToArray();

    public readonly string ToHexString() => Convert.ToHexStringLower(this);

    public readonly bool Equals(Md5Checksum other) => ((ReadOnlySpan<byte>)this).SequenceEqual(other);

    public override readonly bool Equals(object? obj) => obj is Md5Checksum other && Equals(other);
    public override readonly int GetHashCode()
    {
        var hash = new HashCode();
        hash.AddBytes(this);
        return hash.ToHashCode();
    }
    public override readonly string ToString() => ToHexString();

    public static bool operator ==(Md5Checksum left, Md5Checksum right) => left.Equals(right);
    public static bool operator !=(Md5Checksum left, Md5Checksum right) => !left.Equals(right);
}

[InlineArray(Length)]
[JsonConverter(typeof(Sha256ChecksumJsonConverter))]
public struct Sha256Checksum : IEquatable<Sha256Checksum>
{
    public const int Length = 32;
    private byte _element0;

    public static Sha256Checksum FromBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != Length)
            throw new ArgumentException($"SHA-256 checksums must contain exactly {Length} bytes", nameof(bytes));

        var checksum = new Sha256Checksum();
        bytes.CopyTo(checksum);
        return checksum;
    }

    public readonly byte[] ToArray() => ((ReadOnlySpan<byte>)this).ToArray();

    public readonly string ToHexString() => Convert.ToHexStringLower(this);

    public readonly bool Equals(Sha256Checksum other) => ((ReadOnlySpan<byte>)this).SequenceEqual(other);

    public override readonly bool Equals(object? obj) => obj is Sha256Checksum other && Equals(other);
    public override readonly int GetHashCode()
    {
        var hash = new HashCode();
        hash.AddBytes(this);
        return hash.ToHashCode();
    }
    public override readonly string ToString() => ToHexString();

    public static bool operator ==(Sha256Checksum left, Sha256Checksum right) => left.Equals(right);
    public static bool operator !=(Sha256Checksum left, Sha256Checksum right) => !left.Equals(right);
}

public sealed class Md5ChecksumJsonConverter : JsonConverter<Md5Checksum>
{
    public override Md5Checksum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        Md5Checksum checksum = default;
        ChecksumHexJson.Read(ref reader, checksum);
        return checksum;
    }

    public override void Write(Utf8JsonWriter writer, Md5Checksum checksum, JsonSerializerOptions options)
        => ChecksumHexJson.Write(writer, checksum);
}

public sealed class Sha256ChecksumJsonConverter : JsonConverter<Sha256Checksum>
{
    public override Sha256Checksum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        Sha256Checksum checksum = default;
        ChecksumHexJson.Read(ref reader, checksum);
        return checksum;
    }

    public override void Write(Utf8JsonWriter writer, Sha256Checksum checksum, JsonSerializerOptions options)
        => ChecksumHexJson.Write(writer, checksum);
}

internal static class ChecksumHexJson
{
    private const int MaxJsonEscapeLength = 6;

    public static void Read(ref Utf8JsonReader reader, scoped Span<byte> checksum)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("Checksum must be a hexadecimal string");

        if (!reader.HasValueSequence && !reader.ValueIsEscaped)
        {
            Decode(reader.ValueSpan, checksum);
            return;
        }

        int hexLength = checksum.Length * 2;
        long encodedLength = reader.HasValueSequence ? reader.ValueSequence.Length : reader.ValueSpan.Length;
        if (encodedLength > hexLength * MaxJsonEscapeLength)
            throw new JsonException($"Checksum must contain exactly {hexLength} hexadecimal characters");

        Span<byte> hex = stackalloc byte[(int)encodedLength];
        try
        {
            int written = reader.CopyString(hex);
            Decode(hex[..written], checksum);
        }
        catch (ArgumentException ex)
        {
            throw new JsonException($"Checksum must contain exactly {hexLength} hexadecimal characters", ex);
        }
    }

    private static void Decode(ReadOnlySpan<byte> hex, Span<byte> checksum)
    {
        if (hex.Length != checksum.Length * 2
            || Convert.FromHexString(hex, checksum, out _, out _) != OperationStatus.Done)
            throw new JsonException($"Checksum must contain exactly {checksum.Length * 2} hexadecimal characters");
    }

    public static void Write(Utf8JsonWriter writer, ReadOnlySpan<byte> checksum)
    {
        Span<byte> hex = stackalloc byte[checksum.Length * 2];
        if (!Convert.TryToHexStringLower(checksum, hex, out _))
            throw new InvalidOperationException("Could not format checksum as hexadecimal");

        writer.WriteStringValue(hex);
    }
}
