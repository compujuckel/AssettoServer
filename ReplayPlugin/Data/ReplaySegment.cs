using System.Diagnostics.CodeAnalysis;
using System.IO.MemoryMappedFiles;
using DotNext.IO.MemoryMappedFiles;
using JetBrains.Annotations;
using Serilog;

namespace ReplayPlugin.Data;

public class ReplaySegment : IDisposable
{
    public long StartTime;
    public long EndTime;
    public uint StartPlayerInfoIndex;
    public uint EndPlayerInfoIndex;

    private readonly string _path;
    private readonly int _size;
    private MemoryMappedFile? _file;
    private IMappedMemory? _fileAccessor;
    private Memory<byte>? _memory;

    public readonly List<int> Index = [];
    public int Size { get; private set; }

    public ReplaySegment(string path, int size)
    {
        _path = path;
        _size = size;
        Load();
    }

    [MemberNotNull(nameof(_file), nameof(_fileAccessor), nameof(_memory))]
    private void Load()
    {
        Log.Debug("Loading replay segment {0}", _path);
        _file = MemoryMappedFile.CreateFromFile(_path, FileMode.OpenOrCreate, null, _size, MemoryMappedFileAccess.ReadWrite);
        _fileAccessor = _file.CreateMemoryAccessor();
        _memory = _fileAccessor.Memory;
    }
    
    public void Unload()
    {
        Log.Debug("Unloading replay segment {0}", _path);
        _memory = null;
        _fileAccessor?.Dispose();
        _fileAccessor = null;
        _file?.Dispose();
        _file = null;
    }

    [MemberNotNull(nameof(_file), nameof(_fileAccessor), nameof(_memory))]
    private void EnsureLoaded()
    {
        if (_memory == null || _fileAccessor == null || _file == null)
        {
            Load();
        }
    }

    public delegate void ReplayFrameAction<in TState>(ref ReplayFrame frame, TState arg);

    public bool TryAddFrame<TState>(int numCarFrames, int numAiFrames, int numAiMappings, uint playerInfoIndex, TState state, [RequireStaticDelegate] ReplayFrameAction<TState> action)
    {
        EnsureLoaded();
        
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

    public Enumerator GetEnumerator() => new(this);

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
                segment.EnsureLoaded();
                return new ReplayFrame(segment._memory.Value[segment.Index[_i]..]);
            }
        }
    }

    public void Dispose()
    {
        Log.Debug("Disposing replay segment {0}", _path);
        Unload();
        File.Delete(_path);
    }
}
