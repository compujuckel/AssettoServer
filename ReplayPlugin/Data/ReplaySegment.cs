namespace ReplayPlugin.Data;

public class ReplaySegment
{
    public long StartTime;

    private readonly byte[] _array;
    private int _size = 0;

    public List<int> Index = [];
    
    public ReplaySegment(int size = 2_000_000)
    {
        _array = new byte[size];
    }

    public delegate void ReplayFrameAction<in TState>(ref ReplayFrame frame, TState arg);

    public bool TryAddFrame<TState>(int numCarFrames, int numAiFrames, int numAiMappings, TState state, ReplayFrameAction<TState> action)
    {
        var size = ReplayFrame.GetSize(numCarFrames, numAiFrames, numAiMappings);

        if (size > _array.Length - _size)
        {
            return false;
        }

        var mem = _array.AsMemory(_size, size);
        var frame = new ReplayFrame(mem, numCarFrames,  numAiFrames,  numAiMappings);
        
        action(ref frame, state);

        if (_size == 0)
        {
            StartTime = frame.Header.ServerTime;
        }
        
        Index.Add(_size);
        _size += size;
        return true;
    }

    public Enumerator GetEnumerator() => new Enumerator(this);

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
