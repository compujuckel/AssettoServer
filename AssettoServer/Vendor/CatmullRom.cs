using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;

// From https://github.com/JPBotelho/Catmull-Rom-Splines, Unity dependencies removed

namespace JPBotelho
{
    /*  
        Catmull-Rom splines are Hermite curves with special tangent values.
        Hermite curve formula:
        (2t^3 - 3t^2 + 1) * p0 + (t^3 - 2t^2 + t) * m0 + (-2t^3 + 3t^2) * p1 + (t^3 - t^2) * m1
        For points p0 and p1 passing through points m0 and m1 interpolated over t = [0, 1]
        Tangent M[k] = (P[k+1] - P[k-1]) / 2
    */
    public class CatmullRom
    {
        //Struct to keep position, normal and tangent of a spline point
        [System.Serializable]
        public struct CatmullRomPoint
        {
            public Vector3 Position;
            public Vector3 Tangent;
            public Vector3 Normal;

            public CatmullRomPoint(Vector3 position, Vector3 tangent, Vector3 normal)
            {
                Position = position;
                Tangent = tangent;
                Normal = normal;
            }
        }

        private int _resolution; //Amount of points between control points. [Tesselation factor]
        private bool _closedLoop;

        private CatmullRomPoint[] _splinePoints; //Generated spline points

        private Vector3[] _controlPoints;

        //Returns spline points. Count is contorolPoints * resolution + [resolution] points if closed loop.
        public CatmullRomPoint[] GetPoints()
        {
            if(_splinePoints == null)
            {
                throw new System.NullReferenceException("Spline not Initialized!");
            }

            return _splinePoints;
        }

        public CatmullRom(Vector3[] controlPoints, int resolution, bool closedLoop)
        {
            if(controlPoints == null || controlPoints.Length <= 2 || resolution < 2)
            {
                throw new ArgumentException("Catmull Rom Error: Too few control points or resolution too small");
            }

            this._controlPoints = new Vector3[controlPoints.Length];
            for(int i = 0; i < controlPoints.Length; i++)
            {
                this._controlPoints[i] = controlPoints[i];             
            }

            this._resolution = resolution;
            this._closedLoop = closedLoop;

            GenerateSplinePoints();
        }

        //Updates control points
        public void Update(Vector3[] controlPoints)
        {
            if(controlPoints.Length <= 0 || controlPoints == null)
            {
                throw new ArgumentException("Invalid control points");
            }

            this._controlPoints = new Vector3[controlPoints.Length];
            for(int i = 0; i < controlPoints.Length; i++)
            {
                this._controlPoints[i] = controlPoints[i];             
            }

            GenerateSplinePoints();
        }

        //Updates resolution and closed loop values
        public void Update(int resolution, bool closedLoop)
        {
            if(resolution < 2)
            {
                throw new ArgumentException("Invalid Resolution. Make sure it's >= 1");
            }
            this._resolution = resolution;
            this._closedLoop = closedLoop;

            GenerateSplinePoints();
        }

        //Validates if splinePoints have been set already. Throws nullref exception.
        private bool ValidatePoints()
        {
            if(_splinePoints == null)
            {
                throw new NullReferenceException("Spline not initialized!");
            }
            return _splinePoints != null;
        }

        //Sets the length of the point array based on resolution/closed loop.
        private void InitializeProperties()
        {
            int pointsToCreate;
            if (_closedLoop)
            {
                pointsToCreate = _resolution * _controlPoints.Length; //Loops back to the beggining, so no need to adjust for arrays starting at 0
            }
            else
            {
                pointsToCreate = _resolution * (_controlPoints.Length - 1);
            }

            _splinePoints = new CatmullRomPoint[pointsToCreate];       
        }

        //Math stuff to generate the spline points
        private void GenerateSplinePoints()
        {
            InitializeProperties();

            Vector3 p0, p1; //Start point, end point
            Vector3 m0, m1; //Tangents

            // First for loop goes through each individual control point and connects it to the next, so 0-1, 1-2, 2-3 and so on
            int closedAdjustment = _closedLoop ? 0 : 1;
            for (int currentPoint = 0; currentPoint < _controlPoints.Length - closedAdjustment; currentPoint++)
            {
                bool closedLoopFinalPoint = (_closedLoop && currentPoint == _controlPoints.Length - 1);

                p0 = _controlPoints[currentPoint];
                
                if(closedLoopFinalPoint)
                {
                    p1 = _controlPoints[0];
                }
                else
                {
                    p1 = _controlPoints[currentPoint + 1];
                }

                // m0
                if (currentPoint == 0) // Tangent M[k] = (P[k+1] - P[k-1]) / 2
                {
                    if(_closedLoop)
                    {
                        m0 = p1 - _controlPoints[_controlPoints.Length - 1];
                    }
                    else
                    {
                        m0 = p1 - p0;
                    }
                }
                else
                {
                    m0 = p1 - _controlPoints[currentPoint - 1];
                }

                // m1
                if (_closedLoop)
                {
                    if (currentPoint == _controlPoints.Length - 1) //Last point case
                    {
                        m1 = _controlPoints[(currentPoint + 2) % _controlPoints.Length] - p0;
                    }
                    else if (currentPoint == 0) //First point case
                    {
                        m1 = _controlPoints[currentPoint + 2] - p0;
                    }
                    else
                    {
                        m1 = _controlPoints[(currentPoint + 2) % _controlPoints.Length] - p0;
                    }
                }
                else
                {
                    if (currentPoint < _controlPoints.Length - 2)
                    {
                        m1 = _controlPoints[(currentPoint + 2) % _controlPoints.Length] - p0;
                    }
                    else
                    {
                        m1 = p1 - p0;
                    }
                }

                m0 *= 0.5f; //Doing this here instead of  in every single above statement
                m1 *= 0.5f;

                float pointStep = 1.0f / _resolution;

                if ((currentPoint == _controlPoints.Length - 2 && !_closedLoop) || closedLoopFinalPoint) //Final point
                {
                    pointStep = 1.0f / (_resolution - 1);  // last point of last segment should reach p1
                }

                // Creates [resolution] points between this control point and the next
                for (int tesselatedPoint = 0; tesselatedPoint < _resolution; tesselatedPoint++)
                {
                    float t = tesselatedPoint * pointStep;

                    CatmullRomPoint point = Evaluate(p0, p1, m0, m1, t);

                    _splinePoints[currentPoint * _resolution + tesselatedPoint] = point;
                }
            }
        }

        //Evaluates curve at t[0, 1]. Returns point/normal/tan struct. [0, 1] means clamped between 0 and 1.
        public static CatmullRomPoint Evaluate(Vector3 start, Vector3 end, Vector3 tanPoint1, Vector3 tanPoint2, float t)
        {
            Vector3 position = CalculatePosition(start, end, tanPoint1, tanPoint2, t);
            Vector3 tangent = CalculateTangent(start, end, tanPoint1, tanPoint2, t);            
            Vector3 normal = NormalFromTangent(tangent);

            return new CatmullRomPoint(position, tangent, normal);
        }

        //Calculates curve position at t[0, 1]
        public static Vector3 CalculatePosition(Vector3 start, Vector3 end, Vector3 tanPoint1, Vector3 tanPoint2, float t)
        {
            // Hermite curve formula:
            // (2t^3 - 3t^2 + 1) * p0 + (t^3 - 2t^2 + t) * m0 + (-2t^3 + 3t^2) * p1 + (t^3 - t^2) * m1
            Vector3 position = (2.0f * t * t * t - 3.0f * t * t + 1.0f) * start
                + (t * t * t - 2.0f * t * t + t) * tanPoint1
                + (-2.0f * t * t * t + 3.0f * t * t) * end
                + (t * t * t - t * t) * tanPoint2;

            return position;
        }

        //Calculates tangent at t[0, 1]
        public static Vector3 CalculateTangent(Vector3 start, Vector3 end, Vector3 tanPoint1, Vector3 tanPoint2, float t)
        {
            // Calculate tangents
            // p'(t) = (6t² - 6t)p0 + (3t² - 4t + 1)m0 + (-6t² + 6t)p1 + (3t² - 2t)m1
            Vector3 tangent = (6 * t * t - 6 * t) * start
                + (3 * t * t - 4 * t + 1) * tanPoint1
                + (-6 * t * t + 6 * t) * end
                + (3 * t * t - 2 * t) * tanPoint2;

            return Vector3.Normalize(tangent);
        }
        
        //Calculates normal vector from tangent
        public static Vector3 NormalFromTangent(Vector3 tangent)
        {
            return Vector3.Normalize(Vector3.Cross(tangent, Vector3.UnitY) / 2);
        }        
    }
}