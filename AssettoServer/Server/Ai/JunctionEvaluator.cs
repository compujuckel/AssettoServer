using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using AssettoServer.Server.Ai.Structs;

namespace AssettoServer.Server.Ai;

public class JunctionEvaluator
{
    private readonly ConcurrentDictionary<int, bool>? _evaluated;
    public AiCache Cache { get; }

    public JunctionEvaluator(AiCache cache, bool savesState = true)
    {
        Cache = cache;
        
        if (savesState)
            _evaluated = new ConcurrentDictionary<int, bool>();
    }

    public void Clear()
    {
        _evaluated?.Clear();
    }

    public bool WillTakeJunction(int junctionId)
    {
        ref var junction = ref Cache.Junctions[junctionId]; 
        bool result = Random.Shared.NextDouble() < junction.Probability;
        return _evaluated?.GetOrAdd(junctionId, result) ?? result;
    }

    public int Traverse(int pointId, int count = 1)
    {
        return count > 0 ? Next(pointId, count) : Previous(pointId, -count);
    }

    public int Next(int pointId, int count = 1)
    {
        var points = Cache.Points;
        var junctions = Cache.Junctions;
        
        for (int i = 0; i < count && pointId >= 0; i++)
        {
            ref var point = ref points[pointId];
            if (point.JunctionStartId >= 0)
            {
                var junctionId = point.JunctionStartId;

                bool result = WillTakeJunction(junctionId);
                pointId = result ? junctions[junctionId].EndPointId : point.NextId;
            }
            else
            {
                pointId = point.NextId;
            }
        }

        return pointId;
    }

    public bool TryNext(int pointId, out int nextPointId, int count = 1)
    {
        nextPointId = Next(pointId, count);
        return nextPointId >= 0;
    }

    public int Previous(int pointId, int count = 1)
    {
        var points = Cache.Points;
        var junctions = Cache.Junctions;
        
        for (int i = 0; i < count && pointId >= 0; i++)
        {
            ref var point = ref points[pointId];
            if (point.JunctionEndId >= 0)
            {
                var junctionId = point.JunctionEndId;
                ref var junction = ref junctions[junctionId];

                bool result = false;
                if (_evaluated != null && _evaluated.ContainsKey(junctionId))
                {
                    result = _evaluated[junctionId];
                }
                else if (point.PreviousId < 0)
                {
                    result = Random.Shared.NextDouble() < junction.Probability;
                    _evaluated?.TryAdd(junctionId, result);
                }
                    
                pointId = result ? junction.StartPointId : point.PreviousId;
            }
            else
            {
                pointId = point.PreviousId;
            }
        }

        return pointId;
    }
        
    public bool TryPrevious(int pointId, [MaybeNullWhen(false)] out int nextPointId, int count = 1)
    {
        nextPointId = Previous(pointId, count);
        return nextPointId >= 0;
    }
}
