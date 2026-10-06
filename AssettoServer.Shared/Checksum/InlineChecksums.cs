using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AssettoServer.Shared.Checksum;

[InlineArray(16)]
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
        for (int i = 0; i < Length; i++)
            checksum[i] = bytes[i];
        return checksum;
    }

    public byte[] ToArray()
    {
        var bytes = new byte[Length];
        for (int i = 0; i < Length; i++)
            bytes[i] = this[i];
        return bytes;
    }

    public string ToHexString()
    {
        Span<byte> bytes = stackalloc byte[Length];
        for (int i = 0; i < Length; i++)
            bytes[i] = this[i];
        return Convert.ToHexStringLower(bytes);
    }

    public bool Equals(Md5Checksum other)
    {
        for (int i = 0; i < Length; i++)
        {
            if (this[i] != other[i])
                return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is Md5Checksum other && Equals(other);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        for (int i = 0; i < Length; i++)
            hash.Add(this[i]);
        return hash.ToHashCode();
    }
    public override string ToString() => ToHexString();

    public static bool operator ==(Md5Checksum left, Md5Checksum right) => left.Equals(right);
    public static bool operator !=(Md5Checksum left, Md5Checksum right) => !left.Equals(right);
}

[InlineArray(32)]
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
        for (int i = 0; i < Length; i++)
            checksum[i] = bytes[i];
        return checksum;
    }

    public byte[] ToArray()
    {
        var bytes = new byte[Length];
        for (int i = 0; i < Length; i++)
            bytes[i] = this[i];
        return bytes;
    }

    public string ToHexString()
    {
        Span<byte> bytes = stackalloc byte[Length];
        for (int i = 0; i < Length; i++)
            bytes[i] = this[i];
        return Convert.ToHexStringLower(bytes);
    }

    public bool Equals(Sha256Checksum other)
    {
        for (int i = 0; i < Length; i++)
        {
            if (this[i] != other[i])
                return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is Sha256Checksum other && Equals(other);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        for (int i = 0; i < Length; i++)
            hash.Add(this[i]);
        return hash.ToHashCode();
    }
    public override string ToString() => ToHexString();

    public static bool operator ==(Sha256Checksum left, Sha256Checksum right) => left.Equals(right);
    public static bool operator !=(Sha256Checksum left, Sha256Checksum right) => !left.Equals(right);
}

public sealed class Md5ChecksumJsonConverter : InlineChecksumJsonConverter<Md5Checksum>
{
    protected override int Length => Md5Checksum.Length;
    protected override string Name => "MD5";
    protected override Md5Checksum FromBytes(ReadOnlySpan<byte> bytes) => Md5Checksum.FromBytes(bytes);
    protected override byte GetByte(Md5Checksum checksum, int index) => checksum[index];
}

public sealed class Sha256ChecksumJsonConverter : InlineChecksumJsonConverter<Sha256Checksum>
{
    protected override int Length => Sha256Checksum.Length;
    protected override string Name => "SHA256";
    protected override Sha256Checksum FromBytes(ReadOnlySpan<byte> bytes) => Sha256Checksum.FromBytes(bytes);
    protected override byte GetByte(Sha256Checksum checksum, int index) => checksum[index];
}

public abstract class InlineChecksumJsonConverter<TChecksum> : JsonConverter<TChecksum>
    where TChecksum : struct
{
    protected abstract int Length { get; }
    protected abstract string Name { get; }
    protected abstract TChecksum FromBytes(ReadOnlySpan<byte> bytes);
    protected abstract byte GetByte(TChecksum checksum, int index);

    public override TChecksum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException($"{Name} checksum must be a {Length}-byte array");

        Span<byte> bytesArray = stackalloc byte[Length];
        for (int i = 0; i < Length; i++)
        {
            if (!reader.Read() || reader.TokenType != JsonTokenType.Number)
                throw new JsonException($"{Name} checksum array must contain exactly {Length} byte values");

            if (!reader.TryGetByte(out bytesArray[i]))
                throw new JsonException($"{Name} checksum values must be between 0 and 255");
        }

        if (!reader.Read() || reader.TokenType != JsonTokenType.EndArray)
            throw new JsonException($"{Name} checksum array must contain exactly {Length} byte values");

        return FromBytes(bytesArray);
    }

    public override void Write(Utf8JsonWriter writer, TChecksum checksum, JsonSerializerOptions options)
    {
        var serializedBytes = new StringBuilder(Length * 4 + 2);
        serializedBytes.Append('[');
        for (int i = 0; i < Length; i++)
        {
            if (i > 0)
                serializedBytes.Append(", ");
            serializedBytes.Append(GetByte(checksum, i).ToString(CultureInfo.InvariantCulture));
        }
        serializedBytes.Append(']');
        writer.WriteRawValue(serializedBytes.ToString());
    }
}
