using System.Runtime.InteropServices;
using System.Text;
using AssettoServer.Shared.Network.Packets.Incoming;

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

    public string ReadASCIIString(bool bigLength = false)
    {
        short stringLength = bigLength ? Read<short>() : Read<byte>();
            
        var ret = string.Create(stringLength, this, (span, self) => Encoding.ASCII.GetChars(self.Buffer.Slice(self.ReadPosition, span.Length).Span, span));
        ReadPosition += stringLength;

        return ret;
    }

    public string ReadUTF32String(bool bigLength = false)
    {
        short stringLength = bigLength ? Read<short>() : Read<byte>();

        var ret = string.Create(stringLength, this, (span, self) => Encoding.UTF32.GetChars(self.Buffer.Slice(self.ReadPosition, span.Length * 4).Span, span));
        ReadPosition += stringLength * 4;

        return ret;
    }
    
    public string ReadStringFixed(Encoding encoding, int length)
    {
        int bytesToRead = Math.Min(length, Buffer.Length - ReadPosition);
        var ret = encoding.GetString(Buffer.Slice(ReadPosition, bytesToRead).Span);
        ReadPosition += bytesToRead;
        return ret;
    }

    public T Read<T>() where T : unmanaged
    {
        // Marshal.SizeOf(typeof(bool)) returns 4 - we need 1. See https://stackoverflow.com/a/47956291
        var bytesToRead = typeof(T) == typeof(bool) ? 1 : Marshal.SizeOf(typeof(T).IsEnum ? Enum.GetUnderlyingType(typeof(T)) : typeof(T));
        var slice = Buffer.Slice(ReadPosition).Span;

        T result;
        // Workaround for CSP client messages. Zeroes are removed from the end of a message
        if (ReadPosition + bytesToRead > Buffer.Length)
        {
            Span<byte> tmp = stackalloc byte[bytesToRead];
            slice.CopyTo(tmp);
            result = MemoryMarshal.Read<T>(tmp);
        }
        else
        {
            result = MemoryMarshal.Read<T>(slice);
        }
        
        ReadPosition += bytesToRead;
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
