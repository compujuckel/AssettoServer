using System.Net;
using System.Runtime.CompilerServices;
using AssettoServer.Shared.Utils;

namespace AssettoServer.Tests;

public class SocketAddressExtensionsTests
{
    [Test]
    public void Clone_CopiesFamilySizeAndBuffer()
    {
        SocketAddress original = new IPEndPoint(IPAddress.Parse("192.168.1.10"), 9600).Serialize();

        SocketAddress clone = original.Clone();

        Assert.That(clone.Family, Is.EqualTo(original.Family));
        Assert.That(clone.Size, Is.EqualTo(original.Size));
        Assert.That(clone.Buffer.ToArray(), Is.EqualTo(original.Buffer.ToArray()));
    }

    [Test]
    public void Clone_ReturnsIndependentInstance()
    {
        SocketAddress original = new IPEndPoint(IPAddress.Parse("192.168.1.10"), 9600).Serialize();

        SocketAddress clone = original.Clone();
        clone[4] = 10;

        Assert.That(clone, Is.Not.SameAs(original));
        Assert.That(original[4], Is.EqualTo((byte)192));
        Assert.That(clone[4], Is.EqualTo((byte)10));
    }

    [Test]
    public void IpEquals_SameIPv4DifferentPort_ReturnsTrue()
    {
        SocketAddress first = new IPEndPoint(IPAddress.Parse("10.0.0.1"), 9600).Serialize();
        SocketAddress second = new IPEndPoint(IPAddress.Parse("10.0.0.1"), 12000).Serialize();

        Assert.That(first.IpEquals(second), Is.True);
        Assert.That(second.IpEquals(first), Is.True);
    }

    [Test]
    public void IpEquals_DifferentIPv4_ReturnsFalse()
    {
        SocketAddress first = new IPEndPoint(IPAddress.Parse("10.0.0.1"), 9600).Serialize();
        SocketAddress second = new IPEndPoint(IPAddress.Parse("10.0.0.2"), 9600).Serialize();

        Assert.That(first.IpEquals(second), Is.False);
        Assert.That(second.IpEquals(first), Is.False);
    }

    [Test]
    public void IpEquals_ClonedAddress_ReturnsTrue()
    {
        SocketAddress original = new IPEndPoint(IPAddress.Parse("172.16.0.5"), 1).Serialize();

        Assert.That(original.IpEquals(original.Clone()), Is.True);
    }

    [Test]
    public void GetIPv4Address_SameIPv4_ReturnsSameValue()
    {
        SocketAddress first = new IPEndPoint(IPAddress.Parse("203.0.113.7"), 1).Serialize();
        SocketAddress second = new IPEndPoint(IPAddress.Parse("203.0.113.7"), 2).Serialize();

        Assert.That(first.GetIPv4Address(), Is.EqualTo(second.GetIPv4Address()));
    }

    [Test]
    public void GetIPv4Address_DifferentIPv4_ReturnsDifferentValue()
    {
        SocketAddress first = new IPEndPoint(IPAddress.Parse("203.0.113.7"), 1).Serialize();
        SocketAddress second = new IPEndPoint(IPAddress.Parse("203.0.113.8"), 1).Serialize();

        Assert.That(first.GetIPv4Address(), Is.Not.EqualTo(second.GetIPv4Address()));
    }

    [Test]
    public void GetIPv4Address_IgnoresPort()
    {
        SocketAddress first = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1).Serialize();
        SocketAddress second = new IPEndPoint(IPAddress.Parse("127.0.0.1"), ushort.MaxValue).Serialize();

        Assert.That(first.GetIPv4Address(), Is.EqualTo(second.GetIPv4Address()));
    }

    [Test]
    public void ToIPv4String_FormatsDottedDecimal()
    {
        SocketAddress address = new IPEndPoint(IPAddress.Parse("192.168.1.10"), 9600).Serialize();

        Assert.That(address.ToIPv4String(), Is.EqualTo("192.168.1.10"));
    }

    [Test]
    public void ToIPv4String_HandlesBoundaryOctets()
    {
        Assert.That(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 0).Serialize().ToIPv4String(), Is.EqualTo("0.0.0.0"));
        Assert.That(new IPEndPoint(IPAddress.Parse("255.255.255.255"), 0).Serialize().ToIPv4String(), Is.EqualTo("255.255.255.255"));
    }

    [Test]
    public void ToIPv4String_MatchesIPAddressToString()
    {
        string[] samples = ["1.2.3.4", "127.0.0.1", "8.8.8.8", "10.255.0.128"];

        foreach (string sample in samples)
        {
            IPAddress ip = IPAddress.Parse(sample);
            SocketAddress address = new IPEndPoint(ip, 0).Serialize();
            Assert.That(address.ToIPv4String(), Is.EqualTo(ip.ToString()));
        }
    }

    [Test]
    public void GetIPv4Address_MatchesSocketAddressPal()
    {
        string[] samples = ["0.0.0.0", "1.2.3.4", "127.0.0.1", "192.168.1.10", "255.255.255.255", "10.255.0.128"];

        foreach (string sample in samples)
        {
            SocketAddress address = new IPEndPoint(IPAddress.Parse(sample), 9600).Serialize();
            Assert.That(address.GetIPv4Address(), Is.EqualTo(GetIPv4AddressFromPal(null, address.Buffer.Span)), sample);
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "GetIPv4Address")]
    private static extern uint GetIPv4AddressFromPal([UnsafeAccessorType("System.Net.SocketAddressPal, System.Net.Primitives")] object? a, ReadOnlySpan<byte> buffer);
}
