namespace AssettoServer.Server.Ai
{
    public class TrafficSplinePointJunction
    {
        public int DecisionPoint { get; set; }
        public int StartPoint { get; set; }
        
        public TrafficSpline TargetSpline { get; set; }
        public int TargetSplinePoint { get; set; }
        
        public float Probability { get; set; }
        
        public bool IndicateLeft { get; set; }
        public bool IndicateRight { get; set; }
    }
}