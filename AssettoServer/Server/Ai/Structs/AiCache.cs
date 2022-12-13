using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using AssettoServer.Server.Configuration;
using AssettoServer.Utils;
using DotNext.IO.MemoryMappedFiles;
using DotNext.Runtime.InteropServices;
using Serilog;
using Supercluster.KDTree;

namespace AssettoServer.Server.Ai.Structs;

public class AiCache
{
    private readonly MemoryMappedFile _file;
    private readonly IMappedMemoryOwner _pointsAccessor;
    private readonly Pointer<SplinePointStruct> _pointsPointer;
    private readonly IMappedMemoryOwner _junctionsAccessor;
    private readonly Pointer<SplineJunctionStruct> _junctionsPointer;

    public AiCacheHeader Header { get; }
    public Span<SplinePointStruct> Points => _pointsPointer.ToSpan(Header.NumPoints);
    public Span<SplineJunctionStruct> Junctions => _junctionsPointer.ToSpan(Header.NumJunctions);
    public SlowestAiStates SlowestAiStates { get; }
    public KDTree<int> KdTree { get; }
    public int[][] Lanes { get; }
    
    public AiCache(ACServerConfiguration configuration, string path)
    {
        _file = MemoryMappedFile.CreateFromFile(path, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);

        var headerSize = Marshal.SizeOf<AiCacheHeader>();
        
        var hdrMem = _file.CreateMemoryAccessor(0, headerSize, MemoryMappedFileAccess.Read);

        var pointsSize = Marshal.SizeOf<SplinePointStruct>() * Header.NumPoints;
        var junctionsSize = Marshal.SizeOf<SplineJunctionStruct>() * Header.NumJunctions;

        Header = MemoryMarshal.Read<AiCacheHeader>(hdrMem.Bytes);

        _pointsAccessor = _file.CreateMemoryAccessor(headerSize, pointsSize, MemoryMappedFileAccess.Read);
        _pointsPointer = new Pointer<SplinePointStruct>(_pointsAccessor.Pointer.Address);
        
        _junctionsAccessor = _file.CreateMemoryAccessor(headerSize + pointsSize, junctionsSize, MemoryMappedFileAccess.Read);
        _junctionsPointer = new Pointer<SplineJunctionStruct>(_junctionsAccessor.Pointer.Address);

        SlowestAiStates = new SlowestAiStates(Header.NumPoints);
        Lanes = new int[Header.NumPoints][];
        Array.Fill(Lanes, Array.Empty<int>());

        var treeData = new Vector3[Header.NumPoints];
        var points = Points;
        for (int i = 0; i < Header.NumPoints; i++)
        {
            treeData[i] = points[i].Position;
        }
        var treeNodes = Enumerable.Range(0, Header.NumPoints).ToArray();
        
        KdTree = new KDTree<int>(treeData, treeNodes);
        
        for (int i = 0; i < Header.NumPoints; i++)
        {
            if (Lanes[i].Length == 0)
            {
                var lanes = GetLanes(i, configuration.Extra.AiParams.TwoWayTraffic).ToArray();
                foreach (var lane in lanes)
                {
                    Lanes[lane] = lanes;
                }
            }
        }

        Log.Debug("Version: {0}, NumPoints: {1}, NumJunctions: {2}", Header.Version, Header.NumPoints, Header.NumJunctions);
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

    public Vector3 GetForwardVector(int pointId)
    {
        var point = Points[pointId];
        
        if (point.NextId >= 0)
        {
            return Points[point.NextId].Position - point.Position;
        }

        return Vector3.Zero;
    }
    
    public bool IsSameDirection(int pointId1, int pointId2)
    {
        if (pointId1 < 0 || pointId2 < 0) return false;
        return Vector3.Dot(GetForwardVector(pointId1), GetForwardVector(pointId2)) > 0;
    }
    
    public float GetCamber(int pointId, float lerp = 0)
    {
        var point = Points[pointId];
        float camber = point.Camber;
        if (lerp != 0 && point.NextId >= 0)
        {
            camber = (float)MathUtils.Lerp(camber, GetCamber(point.NextId), lerp);
        }

        return camber;
    }
    
    public List<int> GetLanes(int startPointId, bool twoWayTraffic = false)
    {
        var ret = new List<int>();
        const int maxCount = 10;

        var points = Points;

        int point = points[startPointId].LeftId;
        while (point >= 0 && ret.Count < maxCount)
        {
            if (IsSameDirection(startPointId, point))
            {
                if (ret.Contains(point)) break;
                
                ret.Add(point);
                point = points[point].LeftId;
            }
            else if (twoWayTraffic)
            {
                if (ret.Contains(point)) break;
                
                ret.Add(point);
                point = points[point].RightId;
            }
            else
            {
                break;
            }
        }
            
        ret.Reverse();
        ret.Add(startPointId);

        point = points[startPointId].RightId;
        while (point >= 0 && ret.Count < maxCount)
        {
            if (IsSameDirection(startPointId, point))
            {
                if (ret.Contains(point)) break;
                
                ret.Add(point);
                point = points[point].RightId;
            }
            else if (twoWayTraffic)
            {
                if (ret.Contains(point)) break;
                
                ret.Add(point);
                point = points[point].LeftId;
            }
            else
            {
                break;
            }
        }

        if (ret.Count >= maxCount)
        {
            Log.Debug("Possible loop at AI spline point {SplinePointId}", startPointId);
        }

        return ret;
    }
    
    public int RandomLane(int pointId)
    {
        return Lanes[pointId][Random.Shared.Next(Lanes[pointId].Length)];
    }

    public static string GenerateCacheKey(string folder)
    {
        using var algo = MD5.Create();
        
        string aipPath = Path.Join(folder, "fast_lane.aip");
        if (File.Exists(aipPath))
        {
            using var stream = File.OpenRead(aipPath);
            var hash = algo.ComputeHash(stream);
            return Convert.ToHexString(hash);
        }
        else
        {
            var hashStream = new MemoryStream();
            string configPath = Path.Join(folder, "config.yml");
            if (File.Exists(configPath))
            {
                using var stream = File.OpenRead(configPath);
                hashStream.Write(algo.ComputeHash(stream));
            }
            
            if (!Directory.Exists(folder))
            {
                throw new ConfigurationException($"No ai folder found. Please put at least one AI spline fast_lane.ai(p) into {Path.GetFullPath(folder)}");
            }
            
            foreach (string file in Directory.EnumerateFiles(folder, "fast_lane*.ai").OrderBy(f => f))
            {
                using var stream = File.OpenRead(file);
                hashStream.Write(algo.ComputeHash(stream));
            }
            
            var hash = algo.ComputeHash(hashStream);
            return Convert.ToHexString(hash);
        }
    }
}
