using System.Diagnostics.CodeAnalysis;
using System.IO.MemoryMappedFiles;
using DotNext.IO.MemoryMappedFiles;
using JetBrains.Annotations;
using Serilog;

namespace ReplayPlugin.Data;

public sealed class ReplaySegment : IDisposable
{
    public long StartTime { get; private set; }
    public long EndTime { get; private set; }
    public uint StartPlayerInfoIndex { get; private set; }
    public uint EndPlayerInfoIndex { get; private set; }
    public List<int> Index { get; } = [];
    public int Size { get; private set; }
    public bool IsBusy => _lockCount > 0;

    private readonly string _path;
    private readonly int _size;
    private MemoryMappedFile? _file;
    private IMappedMemory? _fileAccessor;
    private Memory<byte>? _memory;
    private int _lockCount;
    private bool _isDisposed;

    [MemberNotNullWhen(true, nameof(_file), nameof(_fileAccessor), nameof(_memory))]
    private bool IsLoaded => _memory != null && _fileAccessor != null && _file != null;
    
    public ReplaySegment(string path, int size)
    {
        _path = path;
        _size = size;
        Load();
    }

    public ReplaySegmentAccessor CreateAccessor()
    {
        return new ReplaySegmentAccessor(this);
    }

    [MemberNotNull(nameof(_file), nameof(_fileAccessor), nameof(_memory))]
    private void Load()
    {
        if (IsLoaded) return;
        
        Log.Debug("Loading replay segment {Path}", _path);
        _file = MemoryMappedFile.CreateFromFile(_path, FileMode.OpenOrCreate, null, _size, MemoryMappedFileAccess.ReadWrite);
        _fileAccessor = _file.CreateMemoryAccessor();
        _memory = _fileAccessor.Memory;
    }

    private bool TryUnload()
    {
        if (_lockCount > 0) return false;
        
        Log.Debug("Unloading replay segment {Path}", _path);
        _memory = null;
        _fileAccessor?.Dispose();
        _fileAccessor = null;
        _file?.Dispose();
        _file = null;
        return true;
    }

    [MemberNotNull(nameof(_file), nameof(_fileAccessor), nameof(_memory))]
    private void ThrowIfUnloaded()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (!IsLoaded)
        {
            throw new InvalidOperationException("Replay segment is not loaded");
        }
    }

    public delegate void ReplayFrameAction<in TState>(ref ReplayFrame frame, TState arg);

    private bool TryAddFrame<TState>(int numCarFrames, int numAiFrames, int numAiMappings, uint playerInfoIndex, TState state, [RequireStaticDelegate, InstantHandle] ReplayFrameAction<TState> action)
    {
        ThrowIfUnloaded();
        
        var size = ReplayFrame.GetSize(numCarFrames, numAiFrames, numAiMappings);

        if (size > _memory.Value.Length - Size)
        {
            return false;
        }

        var mem = _memory.Value.Slice(Size, size);
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

    private Enumerator GetEnumerator() => new(this);

    public ref struct Enumerator(ReplaySegment segment)
    {
        private int _i = -1;

        public bool MoveNext()
        {
            _i++;
            return _i < segment.Index.Count;
        }

        public ReplayFrame Current
        {
            get
            {
                segment.ThrowIfUnloaded();
                return new ReplayFrame(segment._memory.Value[segment.Index[_i]..]);
            }
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        
        if (TryUnload())
        {
            Log.Debug("Disposing replay segment {SegmentPath}", _path);
            File.Delete(_path);
        }
        else
        {
            Log.Error("Cannot dispose and delete replay segment {SegmentPath} because it is locked", _path);
        }

        _isDisposed = true;
    }
    
    public sealed class ReplaySegmentAccessor : IDisposable
    {
        private readonly ReplaySegment _segment;
        private bool _isDisposed;
        
        public bool TryAddFrame<TState>(int numCarFrames, int numAiFrames, int numAiMappings, uint playerInfoIndex, TState state, [RequireStaticDelegate, InstantHandle] ReplayFrameAction<TState> action) 
            => _segment.TryAddFrame(numCarFrames, numAiFrames, numAiMappings, playerInfoIndex, state, action);

        public Enumerator GetEnumerator() => _segment.GetEnumerator();

        public ReplaySegmentAccessor(ReplaySegment segment)
        {
            _segment = segment;
            Interlocked.Increment(ref _segment._lockCount);
            _segment.Load();
        }

        ~ReplaySegmentAccessor() => Dispose();

        public void Dispose()
        {
            if (_isDisposed) return;
            Interlocked.Decrement(ref _segment._lockCount);
            _segment.TryUnload();
            
            _isDisposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
