using System.Text;
using AssettoServer.Network.ClientMessages;
using AssettoServer.Shared.Utils;
using AutoModerationPlugin.Packets;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;
using FastTravelPlugin.Packets;
using ReplayPlugin.Packets;
using TagModePlugin.Packets;
using VotingPresetPlugin.Preset;

namespace AssettoServer.Tests;

public class CSPXxHash3MigrationTests
{
    [Test]
    public void Hash64_MatchesNativeAtLengthAndStripeBoundaries()
    {
        int[] lengths =
        [
            0, 1, 2, 3, 4, 7, 8, 9, 15, 16, 17, 31, 32, 33, 63, 64, 65,
            95, 96, 97, 127, 128, 129, 191, 192, 193, 255, 256, 1023,
            1024, 1025, 2048, 2049, 4095, 4096, 4097
        ];

        foreach (int length in lengths)
        {
            byte[] buffer = new byte[length + 8];
            uint random = 0x9e3779b9;
            for (int i = 0; i < buffer.Length; i++)
            {
                random = unchecked(random * 1664525 + 1013904223);
                buffer[i] = (byte)(random >> 24);
            }

            foreach (int offset in new[] { 0, 1, 3, 7 })
            {
                ReadOnlySpan<byte> input = buffer.AsSpan(offset, length);
                Assert.That(CspXXHash3.Hash64(input), Is.EqualTo(LegacyCppCSPXxHash3.Hash64(input)),
                    $"Length {length}, offset {offset}");
            }
        }
    }

    [Test]
    public void Hash64_MatchesNativeForEveryOnlineEvent()
    {
        Type[] knownEvents =
        [
            typeof(ApiKeyPacket), typeof(CollisionUpdatePacket), typeof(LuaReadyPacket),
            typeof(RequestResetPacket), typeof(TeleportCarPacket), typeof(AutoModerationFlags),
            typeof(FastTravelPacket), typeof(UploadDataPacket), typeof(TagModeColorPacket),
            typeof(ReconnectClientPacket), typeof(TestOnlineEvent)
        ];

        Type[] events = knownEvents.Select(t => t.Assembly)
            .Distinct()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.BaseType is { IsGenericType: true } baseType
                && baseType.GetGenericTypeDefinition() == typeof(OnlineEvent<>))
            .ToArray();

        foreach (Type type in knownEvents)
            Assert.That(events, Does.Contain(type), $"Missing Online Event {type.FullName}");

        Type[] generatorTestMessages =
        [
            typeof(TestMessage1), typeof(TestMessage2), typeof(TestMessage3),
            typeof(TestMessage4), typeof(TestMessage5)
        ];

        foreach (Type type in events.Concat(generatorTestMessages))
        {
            var info = OnlineEventGenerator.ParseClientMessage(type);
            byte[] definition = MakeKeyInput(info.Structure);
            long native = LegacyCppCSPXxHash3.Hash64(definition);
            long managed = CspXXHash3.Hash64(definition);
            uint packetType = unchecked((uint)native ^ (uint)(native >> 32));

            Assert.That(managed, Is.EqualTo(native), $"{type.FullName}: {info.Structure}");
            Assert.That(info.PacketType, Is.EqualTo(packetType), $"{type.FullName}: packet key");

            if (events.Contains(type))
            {
                Type onlineEventType = typeof(OnlineEvent<>).MakeGenericType(type);
                uint actualPacketType = (uint)onlineEventType
                    .GetField(nameof(OnlineEvent<TestOnlineEvent>.PacketType))!.GetValue(null)!;
                Assert.That(actualPacketType, Is.EqualTo(packetType), $"{type.FullName}: initialized event key");
            }
        }
    }

    [Test]
    public void Hash64_DoesNotAllocate()
    {
#if DEBUG
        Assert.Ignore("This test requires optimized assemblies; run this test with -c Release.");
#endif
        foreach (int length in new[] { 0, 3, 8, 16, 53, 129, 4097 })
        {
            byte[] input = CreateInput(length);
            for (int i = 0; i < 2000; i++)
                _ = CspXXHash3.Hash64(input);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 2000; i++)
                _ = CspXXHash3.Hash64(input);

            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero,
                $"Managed hashing allocated for length {length}");
        }
    }

    [Test]
    public void Hash64_BenchmarkManagedAndNativePerformance()
    {
#if DEBUG
        Assert.Ignore("BenchmarkDotNet requires optimized assemblies; run this test with -c Release.");
#endif
        // BenchmarkDotNet 0.15.8 does not recognize the .NET 11 preview SDK for out-of-process jobs.
        var config = ManualConfig.Create(DefaultConfig.Instance)
            .AddJob(Job.ShortRun.WithToolchain(InProcessNoEmitToolchain.Instance));
        var summary = BenchmarkRunner.Run<CSPXxHash3Benchmarks>(config);
        Assert.That(summary.HasCriticalValidationErrors, Is.False);
        Assert.That(summary.Reports.Count(report => report.ResultStatistics != null), Is.EqualTo(6));
    }

    private static byte[] CreateInput(int length)
    {
        byte[] input = new byte[length];
        uint random = 0x9e3779b9;
        for (int i = 0; i < length; i++)
        {
            random = unchecked(random * 1664525 + 1013904223);
            input[i] = (byte)(random >> 24);
        }

        return input;
    }

    private static byte[] MakeKeyInput(string structure)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(structure);
        byte[] result = new byte["server_script"u8.Length + utf8.Length];
        "server_script"u8.CopyTo(result);
        int position = "server_script"u8.Length;

        foreach (byte value in utf8)
        {
            if (!" \f\n\r\t\v"u8.Contains(value))
                result[position++] = value;
        }

        return result.AsSpan(0, position).ToArray();
    }
}

[MemoryDiagnoser]
public class CSPXxHash3Benchmarks
{
    [Params(53, 129, 4097)]
    public int Length { get; set; }

    private byte[] _input = null!;

    [GlobalSetup]
    public void SetUp()
    {
        _input = new byte[Length];
        for (int i = 0; i < _input.Length; i++)
            _input[i] = (byte)(i * 31);
    }

    [Benchmark(Baseline = true)]
    public long Native() => LegacyCppCSPXxHash3.Hash64(_input);

    [Benchmark]
    public long Managed() => CspXXHash3.Hash64(_input);
}
