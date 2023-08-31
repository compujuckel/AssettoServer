using System.Runtime.InteropServices;
using System.Text;
using AssettoServer.Shared.Network.Packets.Outgoing;

namespace AssettoServer.Shared.Network.Packets;

public struct PacketWriter
{
    public readonly Stream? Stream;
    public Memory<byte> Buffer { get; private set; }

    private int _writePosition;
    private readonly bool _rcon = false;

    public PacketWriter(Stream stream, Memory<byte> buffer, bool rcon = false)
    {
        Stream = stream;
        Buffer = buffer;
        _rcon = rcon;
        _writePosition = rcon ? 4 : 2;
            
    }
    public PacketWriter(Memory<byte> buffer)
    {
        Stream = null;
        Buffer = buffer;
        _writePosition = 0;
    }

    public int WritePacket<TPacket>(in TPacket packet) where TPacket : IOutgoingNetworkPacket
    {
        packet.ToWriter(ref this);
        return _writePosition;
    }

    public async ValueTask SendAsync(CancellationToken cancellationToken = default)
    {
        if (Stream == null)
            throw new ArgumentNullException(nameof(Stream));

        if (_rcon)
        {
            Write<byte>(0);
            int packetSize = _writePosition - 4;
            MemoryMarshal.Write(Buffer.Span, ref packetSize);
                
            await Stream.WriteAsync(Buffer.Slice(0, packetSize + 4), cancellationToken);
        }
        else
        {
            ushort packetSize = (ushort)(_writePosition - 2);
            MemoryMarshal.Write(Buffer.Span, ref packetSize);

            await Stream.WriteAsync(Buffer.Slice(0, packetSize + 2), cancellationToken);
        }
    }

    [Obsolete("This function uses UTF8 internally. Use WriteUTF8String instead")]
    public void WriteASCIIString(string? str, bool bigLength = false)
        => WriteUTF8String(str, bigLength);

    public void WriteUTF8String(string? str, bool bigLength = false)
        => WriteString(str, Encoding.UTF8, bigLength ? 2 : 1);

    public void WriteUTF32String(string? str, bool bigLength = false)
        => WriteString(str, Encoding.UTF32, bigLength ? 2: 1);

    public void WriteString(string? str, Encoding encoding, int length = 1)
    {
        str ??= string.Empty;
        
        int bytesWritten = encoding.GetBytes(str, Buffer.Slice(_writePosition + length).Span);

        var networkLength = bytesWritten;

        if (Encoding.UTF32.Equals(encoding))
            networkLength /= 4;
        
        if (length == 1)
            Write((byte)networkLength);
        else if (length == 2)
            Write((ushort)networkLength);
        else if (length == 4)
            Write((uint)networkLength);
        else
            throw new ArgumentOutOfRangeException(nameof(length));
        
        _writePosition += bytesWritten;
    }

    public void WriteStringFixed(string? str, Encoding encoding, int capacity, bool pad = true)
    {
        str ??= string.Empty;

        int bytesWritten = encoding.GetBytes(str, Buffer.Slice(_writePosition, capacity).Span);
        _writePosition += bytesWritten;

        if (pad)
        {
            int remaining = capacity - bytesWritten;
            Buffer.Slice(_writePosition, remaining).Span.Clear();
            _writePosition += remaining;
        }
    }

    public void Write<T>(T value) where T : struct
    {
        WriteBytes(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref value, 1)));
    }

    public void WriteBytes(Span<byte> bytes)
    {
        bytes.CopyTo(Buffer.Slice(_writePosition).Span);
        _writePosition += bytes.Length;
    }
}
