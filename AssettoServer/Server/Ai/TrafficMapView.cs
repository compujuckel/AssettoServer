using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Serilog;

namespace AssettoServer.Server.Ai
{
    public class TrafficMapView
    {
        private TrafficMap _map;

        private readonly ConcurrentDictionary<TrafficSplineJunction, bool> _evaluated = new();

        public TrafficMapView(TrafficMap map)
        {
            _map = map;
        }

        public void Clear()
        {
            _evaluated.Clear();
        }

        public TrafficSplinePoint Next(TrafficSplinePoint point, int count = 1)
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

        public TrafficSplinePoint Previous(TrafficSplinePoint point, int count = 1)
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
    }
}