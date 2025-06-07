using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using AssettoServer.Shared.Network.Packets.Incoming;
using AssettoServer.Shared.Utils;

namespace AssettoServer.Shared.Network.Packets;

public struct PacketReader
{
    public readonly Stream? Stream;
    public Memory<byte> Buffer { get; private set; }
    public int ReadPosition { get; private set; }

    private bool _readPacket;
    private readonly bool _rcon = false;

    public PacketReader(Stream? stream, Memory<byte> buffer, bool rcon = false)
    {
        Stream = stream;
        Buffer = buffer;

        _readPacket = false;
        _rcon = rcon;
        ReadPosition = 0;
    }

    [Obsolete("This function uses UTF8 internally. Use ReadUTF8String instead")]
    public string ReadASCIIString(bool bigLength = false) => ReadUTF8String(bigLength);

    public string ReadUTF8String(bool bigLength = false)
    {
        short stringLength = bigLength ? Read<short>() : Read<byte>();
        
        var ret = Encoding.UTF8.GetString(Buffer.Slice(ReadPosition, stringLength).Span);
        ReadPosition += stringLength;

        return ret;
    }

    public string ReadUTF32String(bool bigLength = false)
    {
        short stringLength = bigLength ? Read<short>() : Read<byte>();
        
        var ret = Encoding.UTF32.GetString(Buffer.Slice(ReadPosition, stringLength * 4).Span);
        ReadPosition += stringLength * 4;
        
        return ret;
    }
    
    public string ReadStringFixed(Encoding encoding, int length)
    {
        int bytesToRead = Math.Min(length, Buffer.Length - ReadPosition);
        var ret = encoding.GetString(Buffer.Slice(ReadPosition, bytesToRead).Span.TrimEnd(stackalloc byte[] { 0 }));
        ReadPosition += bytesToRead;
        return ret;
    }

    public Span<T> ReadArrayFixed<T>(int length) where T : unmanaged
    {
        int bytesToRead = Math.Min(MarshalUtils.SizeOf(typeof(T).IsEnum ? Enum.GetUnderlyingType(typeof(T)) : typeof(T)) * length, Buffer.Length - ReadPosition);
        var ret = MemoryMarshal.Cast<byte, T>(Buffer.Slice(ReadPosition, bytesToRead).Span);
        ReadPosition += bytesToRead;
        return ret;
    }

    public Color ReadRgbmAsColor()
    {
        var r = (byte) (Read<float>() * 255);
        var g = (byte) (Read<float>() * 255);
        var b = (byte) (Read<float>() * 255);
        var m = (byte) (Read<float>() * 255);

        return Color.FromArgb(m, r, g, b);
    }

    public T Read<T>() where T : unmanaged
    {
        var bytesToRead = MarshalUtils.SizeOf(typeof(T).IsEnum ? Enum.GetUnderlyingType(typeof(T)) : typeof(T));
        var actualBytesRead = Math.Min(bytesToRead, Buffer.Length - ReadPosition);
        var slice = Buffer.Slice(ReadPosition, actualBytesRead).Span;

        T result;
        // Workaround for CSP client messages. Zeroes are removed from the end of a message
        if (ReadPosition + bytesToRead > Buffer.Length)
        {
            Span<byte> tmp = stackalloc byte[bytesToRead];
            slice.CopyTo(tmp);
            result = MemoryMarshal.Read<T>(tmp);
            ReadPosition += actualBytesRead;
        }
        else
        {
            result = MemoryMarshal.Read<T>(slice);
            ReadPosition += bytesToRead;
        }
        
        return result;
    }

    public void ReadBytes(Memory<byte> buffer)
    {
        Buffer.Slice(ReadPosition, buffer.Length).CopyTo(buffer);
        ReadPosition += buffer.Length;
    }

    public TPacket ReadPacket<TPacket>() where TPacket : IIncomingNetworkPacket, new()
    {
        TPacket packet = new TPacket();
        packet.FromReader(this);

        return packet;
    }

    public async ValueTask<int> ReadPacketAsync(CancellationToken cancellationToken = default)
    {
        if (_readPacket)
            return 0;

        _readPacket = true;

        int packetSize = 0;
        if (_rcon)
        {
            if (await ReadBytesInternalAsync(Buffer.Slice(0, 4), cancellationToken))
            {
                packetSize = MemoryMarshal.Read<int>(Buffer.Span);
            }
        }
        else
        {
            if (await ReadBytesInternalAsync(Buffer.Slice(0, 2), cancellationToken))
            {
                packetSize = MemoryMarshal.Read<ushort>(Buffer.Span);
            }
        }

        if (packetSize > Buffer.Length)
        {
            Buffer.Span.Clear();
            return 0;
        }

        SliceBuffer(packetSize);

        await ReadBytesInternalAsync(Buffer, cancellationToken);

        return packetSize;
    }

    public void SliceBuffer(int newSize)
    {
        Buffer = Buffer.Slice(_rcon ? 4 : 2, newSize);
    }

    private async ValueTask<bool> ReadBytesInternalAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(Stream);
        return await Stream.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, cancellationToken) == buffer.Length;
    }
}
