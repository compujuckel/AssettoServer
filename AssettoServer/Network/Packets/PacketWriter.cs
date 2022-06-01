using AssettoServer.Network.Packets.Outgoing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AssettoServer.Network.Packets
{
    public struct PacketWriter
    {
        public readonly Stream? Stream;
        public Memory<byte> Buffer { get; private set; }

        private int _writePosition;

        public PacketWriter(Stream stream, Memory<byte> buffer)
        {
            Stream = stream;
            Buffer = buffer;
            _writePosition = 2;
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
            
            ushort packetSize = (ushort)(_writePosition - 2);
            MemoryMarshal.Write(Buffer.Span, ref packetSize);

            await Stream.WriteAsync(Buffer.Slice(0, packetSize + 2), cancellationToken);
        }

        public void WriteASCIIString(string? str, bool bigLength = false)
            => WriteString(str, Encoding.ASCII, bigLength ? 2 : 1);

        public void WriteUTF32String(string? str, bool bigLength = false)
            => WriteString(str, Encoding.UTF32, bigLength ? 2 : 1);

        public void WriteString(string? str, Encoding encoding, int length = 1)
        {
            str ??= string.Empty;

            if (length == 1)
                Write((byte)str.Length);
            else if (length == 2)
                Write((ushort)str.Length);
            else if (length == 4)
                Write((uint)str.Length);
            else
                throw new ArgumentOutOfRangeException(nameof(length));

            int bytesWritten = encoding.GetBytes(str, Buffer.Slice(_writePosition).Span);
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
                Buffer.Slice(_writePosition, remaining).Span.Fill(0);
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
}
