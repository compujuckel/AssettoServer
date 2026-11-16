using System.Net;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Net.Sockets;

namespace AssettoServer.Shared.Utils;

public static class SocketAddressExtensions
{
    extension(SocketAddress address)
    {
        public SocketAddress Clone()
        {
            var clone = new SocketAddress(address.Family, address.Size);
            address.Buffer.CopyTo(clone.Buffer);
            return clone;
        }

        public bool IpEquals(SocketAddress other)
        {
            return address.GetIPv4Address() == other.GetIPv4Address();
        }

        public uint GetIPv4Address()
        {
            Debug.Assert(address.Family == AddressFamily.InterNetwork);
            return BinaryPrimitives.ReadUInt32LittleEndian(address.Buffer.Span.Slice(4, 4));
        }
        
        public string ToIPv4String()
        {
            Debug.Assert(address.Family == AddressFamily.InterNetwork);
            return $"{address[4]}.{address[5]}.{address[6]}.{address[7]}";
        }
    }
}
