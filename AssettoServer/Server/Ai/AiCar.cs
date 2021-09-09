using System;
using System.Numerics;
using AssettoServer.Network.Packets.Shared;
using Serilog;

namespace AssettoServer.Server.Ai
{
    public class AiCar : EntryCar
    {

        public long SpawnProtectionEnds { get; set; }
        public int AiSplinePosition { get; private set; }
        
        private long _lastTick = Environment.TickCount64;

        private const float Speed = 80 / 3.6f;

        private Vector3 _currentVec;
        private Vector3 _currentVecNormal;
        private Vector3 _currentVecProgress;

        private bool _initialized = false;
        
        public AiCar()
        {
            Log.Debug("Added AI car");
        }

        public void MoveToSplinePosition(int splinePos, bool forceUpdate = false)
        {
            AiSplinePosition = splinePos;
            Vector3 currentPos = Server.AiSpline.IdealLine[splinePos].Pos;
            Vector3 nextPos = Server.AiSpline.IdealLine[(splinePos + 1) % Server.AiSpline.IdealLine.Length].Pos;
            _currentVec = Vector3.Subtract(nextPos, currentPos);
            _currentVecNormal = Vector3.Normalize(_currentVec);
            _currentVecProgress = Vector3.Zero;

            if (forceUpdate)
            {
                Update();
            }
        }

        public void Update()
        {
            if (!_initialized)
            {
                AiSplinePosition = new Random().Next(0, Server.AiSpline.IdealLine.Length);
                
                MoveToSplinePosition(AiSplinePosition);
                _initialized = true;
            }
            
            long dt = Environment.TickCount64 - _lastTick;
            _lastTick = Environment.TickCount64;

            float moveMeters = (dt / 1000.0f) * Speed;
            
            _currentVecProgress += _currentVecNormal * moveMeters;
            
            //Log.Debug("dt {0} m {1} cur {2} prog {3} pl {4} cl {5}", dt, moveMeters, _currentVec, _currentVecProgress, _currentVecProgress.Length(), _currentVec.Length());
            if (_currentVecProgress.Length() > _currentVec.Length())
            {
                AiSplinePosition++;
                if (AiSplinePosition >= Server.AiSpline.IdealLine.Length)
                {
                    AiSplinePosition = 0;
                }
                //Log.Debug("next spline pos {0}", _aiSplinePosition);
                
                MoveToSplinePosition(AiSplinePosition);
            }
            
            Vector3 rotation = new Vector3()
            {
                X = (float)(Math.Atan2(_currentVec.Z, _currentVec.X) - Math.PI / 2),
                //Y = (float)(Math.Acos(_currentVecNormal.Y))
                //Z = (float)Math.Acos(direction.Z)
            };

            //Log.Debug("cur {0}, nxt {1}, rot {2}", currentPos, nextPos, rotation);

            UpdatePosition(new PositionUpdate()
            {
                PakSequenceId = (byte)(Status.PakSequenceId + 1),
                Timestamp = (uint)(Environment.TickCount - Server.StartTime),
                LastRemoteTimestamp = (uint)(Environment.TickCount - Server.StartTime),
                Position = Server.AiSpline.IdealLine[AiSplinePosition].Pos + _currentVecProgress,
                Rotation = rotation,
                Velocity = Vector3.Multiply(Vector3.Normalize(_currentVec), Speed),
                SteerAngle = 127,
                WheelAngle = 127,
                TyreAngularSpeedFL = 100,
                TyreAngularSpeedFR = 100,
                TyreAngularSpeedRL = 100,
                TyreAngularSpeedRR = 100,
                EngineRpm = 3000
            });
        }
    }
}