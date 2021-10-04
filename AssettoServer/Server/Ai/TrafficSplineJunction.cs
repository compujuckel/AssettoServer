using System;
using System.Collections.Generic;

namespace AssettoServer.Server.Ai
{
    public class TrafficSplineJunction
    {
        public TrafficSplinePoint StartPoint { get; set; }
        public TrafficSplinePoint EndPoint { get; set; }
        public float Probability { get; set; }

        private Dictionary<AiState, bool> _evaluated = new Dictionary<AiState, bool>();
        private Random _random = new Random();

        public bool Evaluate(AiState aiState)
        {
            if (_evaluated.ContainsKey(aiState))
            {
                return _evaluated[aiState];
            }
            
            bool result = _random.NextDouble() < Probability;
            _evaluated.Add(aiState, result);
            
            return result;
        }

        public void Reset(AiState aiState)
        {
            if (_evaluated.ContainsKey(aiState))
            {
                _evaluated.Remove(aiState);
            }
        }
    }
}