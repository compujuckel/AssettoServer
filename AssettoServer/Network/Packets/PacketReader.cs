using AssettoServer.Network.Packets.Incoming;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AssettoServer.Utils;

namespace AssettoServer.Network.Packets;

[NonCopyable]
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
        
        var ret = Encoding.ASCII.GetString(Buffer.Slice(ReadPosition, stringLength).Span);
        ReadPosition += stringLength;

        return ret;
    }

    public string ReadUTF32String()
    {
        byte stringLength = Read<byte>();
        
        var ret = Encoding.UTF32.GetString(Buffer.Slice(ReadPosition, stringLength * 4).Span);
        ReadPosition += stringLength * 4;

        return ret;
    }

    public T Read<T>() where T : unmanaged
    {
        T result = MemoryMarshal.Read<T>(Buffer.Slice(ReadPosition).Span);
        ReadPosition += Marshal.SizeOf(typeof(T).IsEnum ? Enum.GetUnderlyingType(typeof(T)) : typeof(T));

        return result;
    }

    public void ReadBytes(Memory<byte> buffer)
    {
        Buffer.Slice(ReadPosition, buffer.Length).CopyTo(buffer);
        ReadPosition += buffer.Length;
    }

    public TPacket ReadPacket<TPacket>() where TPacket : struct, IIncomingNetworkPacket
    {
        TPacket packet = default;
        packet.FromReader(ref this);

        return packet;
    }

    public async ValueTask<int> ReadPacketAsync(CancellationToken cancellationToken = default)
    {
        if (_readPacket)
            return 0;

        _readPacket = true;

        int packetSize;
        if (_rcon)
        {
            await ReadBytesInternalAsync(Buffer.Slice(0, 4), cancellationToken);
            packetSize = MemoryMarshal.Read<int>(Buffer.Span);
        }
        else
        {
            await ReadBytesInternalAsync(Buffer.Slice(0, 2), cancellationToken);
            packetSize = MemoryMarshal.Read<ushort>(Buffer.Span);
        }
            
        if (packetSize > Buffer.Length)
        {
            Buffer.Span.Clear();
            return 0;
        }

        Buffer = Buffer.Slice(0, packetSize);

        await ReadBytesInternalAsync(Buffer, cancellationToken);

        return packetSize;
    }

    internal void SliceBuffer(int newSize)
    {
        Buffer = Buffer.Slice(0, newSize);
    }

    private async ValueTask ReadBytesInternalAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (Stream == null)
            throw new ArgumentNullException(nameof(Stream));

        int totalBytesRead = 0;
        int bytesRead;
        int bufferLength = buffer.Length;
            
        while ((bytesRead = await Stream.ReadAsync(buffer.Slice(totalBytesRead), cancellationToken)) > 0 && (totalBytesRead += bytesRead) < bufferLength) { }
    }
}
