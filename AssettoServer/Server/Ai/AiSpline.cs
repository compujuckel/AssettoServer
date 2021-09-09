using System.IO;
using System.Numerics;
using Serilog;

namespace AssettoServer.Server.Ai
{
    public class AiSpline
    {
        public struct AiSplinePoint
        {
            public Vector3 Pos;
            public float Dist;
            public int Id;
        }

        public AiSplinePoint[] IdealLine;
        public int Header;
        public int DetailCount;
        public int U1;
        public int U2;
        
        public int WorldToSpline(Vector3 position)
        {
            int splinePos = 0;
            float minDistance = float.MaxValue;
            for (var i = 0; i < IdealLine.Length; i++)
            {
                float dist = Vector3.DistanceSquared(position, IdealLine[i].Pos);
                if (dist < minDistance)
                {
                    splinePos = i;
                    minDistance = dist;
                }
            }

            return splinePos;
        }

        public Vector3 SplineToWorld(int splinePos)
        {
            return IdealLine[splinePos].Pos;
        }

        public static AiSpline FromFile(string path)
        {
            Log.Debug("Loading AI spline {0}", path);
            using var reader = new BinaryReader(File.OpenRead(path));

            var ret = new AiSpline
            {
                Header = reader.ReadInt32(),
                DetailCount = reader.ReadInt32(),
                U1 = reader.ReadInt32(),
                U2 = reader.ReadInt32()
            };
            
            Log.Debug("Header {0}, DetailCount {1}", ret.Header, ret.DetailCount);

            ret.IdealLine = new AiSplinePoint[ret.DetailCount];

            for (var i = 0; i < ret.DetailCount; i++)
            {
                var p = new AiSplinePoint
                {
                    Pos =
                    {
                        X = reader.ReadSingle(),
                        Y = reader.ReadSingle(),
                        Z = reader.ReadSingle()
                    },
                    Dist = reader.ReadSingle(),
                    Id = reader.ReadInt32()
                };
                
                //Log.Debug("{0}: X {1} Y {2} Z {3} D {4}", p.Id, p.Pos.X, p.Pos.Y, p.Pos.Z, p.Dist);
                ret.IdealLine[i] = p;
            }

            return ret;
        }
    }
}