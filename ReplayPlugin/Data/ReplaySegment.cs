namespace ReplayPlugin.Data;

public class ReplaySegment
{
    public long StartTime;
    public long EndTime;

    private readonly byte[] _array;

    public readonly List<int> Index = [];
    public readonly int MaxSize;
    public int Size { get; private set; } = 0;

    public ReplaySegment(int size)
    {
        MaxSize = size;
        _array = new byte[size];
    }

    public delegate void ReplayFrameAction<in TState>(ref ReplayFrame frame, TState arg);

    public bool TryAddFrame<TState>(int numCarFrames, int numAiFrames, int numAiMappings, TState state, ReplayFrameAction<TState> action)
    {
        var size = ReplayFrame.GetSize(numCarFrames, numAiFrames, numAiMappings);

        if (size > _array.Length - Size)
        {
            return false;
        }

        var mem = _array.AsMemory(Size, size);
        var frame = new ReplayFrame(mem, numCarFrames,  numAiFrames,  numAiMappings);
        
        action(ref frame, state);

        if (Size == 0)
        {
            StartTime = frame.Header.ServerTime;
        }

        EndTime = frame.Header.ServerTime;
        
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
