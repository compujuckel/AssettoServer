using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ReplayPlugin.Data;

public readonly ref struct ReplayFrame
{
    public readonly ref ReplayFrameHeader Header;
    public readonly Span<ReplayCarFrame> CarFrames;
    public readonly Span<ReplayCarFrame> AiFrames;
    public readonly Span<short> AiMappings;

    private static readonly int HeaderSize = Unsafe.SizeOf<ReplayFrameHeader>();
    private static readonly int CarFrameSize = Unsafe.SizeOf<ReplayCarFrame>();
    private const int AiMappingSize = sizeof(short);

    public ReplayFrame(Memory<byte> memory)
    {
        Header = ref MemoryMarshal.Cast<byte, ReplayFrameHeader>(memory.Span)[0];
        CarFrames = MemoryMarshal.Cast<byte, ReplayCarFrame>(memory.Span.Slice(HeaderSize, Header.CarFrameCount * CarFrameSize));
        AiFrames = MemoryMarshal.Cast<byte, ReplayCarFrame>(memory.Span.Slice(HeaderSize + Header.CarFrameCount * CarFrameSize, Header.AiFrameCount * CarFrameSize));
        AiMappings = MemoryMarshal.Cast<byte, short>(memory.Span.Slice(HeaderSize + (Header.CarFrameCount + Header.AiFrameCount) * CarFrameSize, Header.AiMappingCount * AiMappingSize));
    }
    
    public ReplayFrame(Memory<byte> memory, int numCarFrames, int numAiFrames, int numAiMappings, uint playerInfoIndex)
    {
        Header = ref MemoryMarshal.Cast<byte, ReplayFrameHeader>(memory.Span)[0];
        Header.CarFrameCount = (byte)numCarFrames;
        Header.AiFrameCount = (ushort)numAiFrames;
        Header.AiMappingCount = (ushort)numAiMappings;
        Header.PlayerInfoIndex = playerInfoIndex;

        CarFrames = MemoryMarshal.Cast<byte, ReplayCarFrame>(memory.Span.Slice(HeaderSize, Header.CarFrameCount * CarFrameSize));
        AiFrames = MemoryMarshal.Cast<byte, ReplayCarFrame>(memory.Span.Slice(HeaderSize + Header.CarFrameCount * CarFrameSize, Header.AiFrameCount * CarFrameSize));
        AiMappings = MemoryMarshal.Cast<byte, short>(memory.Span.Slice(HeaderSize + (Header.CarFrameCount + Header.AiFrameCount) * CarFrameSize, Header.AiMappingCount * AiMappingSize));
    }

    public static int GetSize(int numCarFrames, int numAiFrames, int numAiMappings)
    {
        return HeaderSize + (numCarFrames + numAiFrames) * CarFrameSize + numAiMappings * AiMappingSize;
    }

    public Span<short> GetAiFrameMappings(int index)
    {
        int len = AiMappings[index];
        return AiMappings.Slice(index + 1, len);
    }
}
