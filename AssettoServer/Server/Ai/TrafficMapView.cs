using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace AssettoServer.Server.Ai;

public class TrafficMapView
{
    private readonly ConcurrentDictionary<TrafficSplineJunction, bool> _evaluated = new();

    public void Clear()
    {
        _evaluated.Clear();
    }

    public TrafficSplinePoint? Next(TrafficSplinePoint? point, int count = 1)
    {
        for (int i = 0; i < count && point != null; i++)
        {
            if (point.JunctionStart != null)
            {
                var junction = point.JunctionStart;

                bool result = _evaluated.GetOrAdd(junction, _ => Random.Shared.NextDouble() < junction.Probability);
                point = result ? junction.EndPoint : point.Next;
            }
            else
            {
                point = point.Next;
            }
        }

        return point;
    }

    public bool TryNext(TrafficSplinePoint? point, [MaybeNullWhen(false)] out TrafficSplinePoint nextPoint, int count = 1)
    {
        nextPoint = Next(point, count);
        return nextPoint != null;
    }

    public TrafficSplinePoint? Previous(TrafficSplinePoint? point, int count = 1)
    {
        for (int i = 0; i < count && point != null; i++)
        {
            if (point.JunctionEnd != null)
            {
                var junction = point.JunctionEnd;

                bool result = false;
                if (_evaluated.ContainsKey(junction))
                {
                    result = _evaluated[junction];
                }
                else if (point.Previous == null)
                {
                    result = Random.Shared.NextDouble() < junction.Probability;
                    _evaluated.TryAdd(junction, result);
                }
                    
                point = result ? junction.StartPoint : point.Previous;
            }
            else
            {
                point = point.Previous;
            }
        }

        return point;
    }
        
    public bool TryPrevious(TrafficSplinePoint? point, [MaybeNullWhen(false)] out TrafficSplinePoint nextPoint, int count = 1)
    {
        nextPoint = Previous(point, count);
        return nextPoint != null;
    }
}
