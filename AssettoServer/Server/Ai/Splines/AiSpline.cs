using System;
using System.Buffers;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Numerics;
using System.Runtime.InteropServices;
using AssettoServer.Utils;
using DotNext.IO.MemoryMappedFiles;
using DotNext.Runtime.InteropServices;
using Serilog;
using Supercluster.KDTree;

namespace AssettoServer.Server.Ai.Splines;

public class AiSpline : IDisposable
{
    public const int SupportedVersion = 1;
    
    private readonly MemoryMappedFile _file;
    private readonly IMappedMemory _fileAccessor;
    private readonly Pointer<SplinePoint> _pointsPointer;
    private readonly Pointer<SplineJunction> _junctionsPointer;
    private readonly IMemoryOwner<Vector3> _treePointsOwner;
    private readonly IMemoryOwner<int> _treeNodesOwner;
    private readonly nint _lanesOffset;

    public AiSplineHeader Header { get; }
    public ReadOnlySpan<SplinePoint> Points => _pointsPointer.AsSpan(Header.NumPoints);
    public ReadOnlySpan<SplineJunction> Junctions => _junctionsPointer.AsSpan(Header.NumJunctions);
    public SlowestAiStates SlowestAiStates { get; }
    public KDTree<int> KdTree { get; }
    public SplinePointOperations Operations => new(Points);
    
    public AiSpline(string path)
    {
        Log.Debug("Mapping cached AI spline {Path} to memory", Path.GetFileName(path));
        
        _file = MemoryMappedFile.CreateFromFile(path, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
        _fileAccessor = _file.CreateMemoryAccessor(0, 0, MemoryMappedFileAccess.Read);
        _fileAccessor.Prefault();
        
        nint offset = 0;
        
        Header = MemoryMarshal.Read<AiSplineHeader>(_fileAccessor.Bytes);
        offset += Marshal.SizeOf<AiSplineHeader>();

        if (Header.Version != SupportedVersion)
        {
            throw new InvalidOperationException("Unsupported AI cache file version");
        }

        _pointsPointer = new Pointer<SplinePoint>(_fileAccessor.Pointer.Address + offset);
        offset += Marshal.SizeOf<SplinePoint>() * Header.NumPoints;
        
        _junctionsPointer = new Pointer<SplineJunction>(_fileAccessor.Pointer.Address + offset);
        offset += Marshal.SizeOf<SplineJunction>() * Header.NumJunctions;
        
        _treePointsOwner = new Pointer<Vector3>(_fileAccessor.Pointer.Address + offset).ToMemoryOwner(Header.NumKdTreePoints);
        offset += Marshal.SizeOf<Vector3>() * Header.NumKdTreePoints;
        
        _treeNodesOwner = new Pointer<int>(_fileAccessor.Pointer.Address + offset).ToMemoryOwner(Header.NumKdTreePoints);
        offset += Marshal.SizeOf<int>() * Header.NumKdTreePoints;

        _lanesOffset = offset;
        
        KdTree = new KDTree<int>(_treePointsOwner.Memory, _treeNodesOwner.Memory, Header.NumPoints);
        SlowestAiStates = new SlowestAiStates(Header.NumPoints);
    }

    public ReadOnlySpan<int> GetLanes(int pointId)
    {
        if (pointId < 0) return ReadOnlySpan<int>.Empty;
        var lanesId = Points[pointId].LanesId;
        if (lanesId < 0) return ReadOnlySpan<int>.Empty;
        var offset = _fileAccessor.Pointer.Address + _lanesOffset + lanesId;
        var count = new Pointer<int>(offset).Value;
        return new Pointer<int>(offset + sizeof(int)).AsSpan(count);
    }
    
    public (int PointId, float DistanceSquared) WorldToSpline(Vector3 position)
    {
        var nearest = KdTree.NearestNeighbors(position, 1);
        if (nearest.Length == 0)
        {
            return (-1, float.PositiveInfinity);
        }

        float dist = Vector3.DistanceSquared(position, Points[nearest[0].Item2].Position);
        return (nearest[0].Item2, dist);
    }

    public int RandomLane(int pointId)
    {
        var lanes = GetLanes(pointId);
        return lanes[Random.Shared.Next(lanes.Length)];
    }

    public void Dispose()
    {
        _treeNodesOwner.Dispose();
        _treePointsOwner.Dispose();
        _fileAccessor.Dispose();
        _file.Dispose();
    }
}
