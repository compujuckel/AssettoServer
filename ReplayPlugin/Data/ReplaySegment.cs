using JetBrains.Annotations;

namespace ReplayPlugin.Data;

public class ReplaySegment
{
    public long StartTime;
    public long EndTime;
    public uint StartPlayerInfoIndex;
    public uint EndPlayerInfoIndex;

    private readonly byte[] _array;

    public readonly List<int> Index = [];
    public int Size { get; private set; }

    public ReplaySegment(int size)
    {
        _array = new byte[size];
    }

    public delegate void ReplayFrameAction<in TState>(ref ReplayFrame frame, TState arg);

    public bool TryAddFrame<TState>(int numCarFrames, int numAiFrames, int numAiMappings, uint playerInfoIndex, TState state, [RequireStaticDelegate] ReplayFrameAction<TState> action)
    {
        var size = ReplayFrame.GetSize(numCarFrames, numAiFrames, numAiMappings);

        if (size > _array.Length - Size)
        {
            return false;
        }

        var mem = _array.AsMemory(Size, size);
        var frame = new ReplayFrame(mem, numCarFrames,  numAiFrames, numAiMappings, playerInfoIndex);
        
        action(ref frame, state);

        if (Size == 0)
        {
            StartTime = frame.Header.ServerTime;
            StartPlayerInfoIndex = frame.Header.PlayerInfoIndex;
        }

        EndTime = frame.Header.ServerTime;
        EndPlayerInfoIndex = frame.Header.PlayerInfoIndex;
        
        Index.Add(Size);
        Size += size;
        return true;
    }

    public Enumerator GetEnumerator() => new(this);

    public ref struct Enumerator(ReplaySegment segment)
    {
        private int _i = -1;

        public bool MoveNext()
        {
            _i++;
            return _i < segment.Index.Count;
        }

        public ReplayFrame Current => new(segment._array.AsMemory(segment.Index[_i]));
    }
}
