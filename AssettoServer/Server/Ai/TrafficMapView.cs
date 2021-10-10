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
        private readonly Random _random = new();
        
        public TrafficSplinePoint CurrentSplinePoint { get; private set; }

        public TrafficMapView(TrafficMap map)
        {
            _map = map;
        }

        public void Teleport(TrafficSplinePoint point)
        {
            CurrentSplinePoint = point;
            _evaluated.Clear();
        }

        public TrafficSplinePoint Traverse(int count = 1)
        {
            CurrentSplinePoint = Peek(count);
            return CurrentSplinePoint;
        }

        public TrafficSplinePoint Peek(int count = 1)
        {
            var point = CurrentSplinePoint;
            for (int i = 0; i < count && point != null; i++)
            {
                if (point.JunctionStart != null)
                {
                    var junction = point.JunctionStart;

                    bool result = _evaluated.GetOrAdd(junction, (key) => _random.NextDouble() < junction.Probability);
                    point = result ? junction.EndPoint : point.Next;
                }
                else
                {
                    point = point.Next;
                }
            }

            return point;
        }

        public TrafficSplinePoint PeekBehind(int count = 1)
        {
            var point = CurrentSplinePoint;
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
                    /*else
                    {
                        result = _random.NextDouble() < junction.Probability;
                        _evaluated.Add(junction, result);
                    }*/

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