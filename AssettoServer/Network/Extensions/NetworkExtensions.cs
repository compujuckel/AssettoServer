using AssettoServer.Network.Packets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AssettoServer.Network.Extensions
{
    public static class NetworkExtensions
    {
        public static async ValueTask<PacketReader> CreatePacketReaderAsync(this Stream stream, Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            PacketReader reader = new PacketReader(stream, buffer);
            reader.SliceBuffer(await reader.ReadPacketAsync(cancellationToken));

            return reader;
        }
    }
}
