using System.Collections;
using System.Collections.Generic;

using UnityEngine;

namespace UnityFramework
{
    [System.Serializable]
    public struct Vector2CurveHanldeData
    {
        public Vector2 inHandle;
        public Vector2 outHandle;
    }

    [System.Serializable]
    public class Vector2CurveExtended : Vector2Curve
    {
        public enum CurveMode
        {
            Hermite,
            Catmull_Rom,
        }


        public enum CurvePlayeMode
        {
            Once,
            Loop,
            PingPong,
        }

        [SerializeField] CurveMode curveMode = CurveMode.Catmull_Rom;
        [SerializeField] CurvePlayeMode curvePlayeMode = CurvePlayeMode.Loop;
        [SerializeField] Vector2CurveHanldeData[] vector2CurveHanldeDatas;
        [SerializeField] float testValue = 1.0f;

        public Vector2CurveExtended() : base()
        {

        }

        public override Vector2 Evaluate(float t)
        {
            switch (curvePlayeMode)
            {
                case CurvePlayeMode.Loop:
                    t = t - Mathf.Floor(t);
                    break;
                case CurvePlayeMode.PingPong:

                    int cycle = (int)t;
                    float frac = t - cycle;
                    // 비트 연산으로 홀수 짝수 검사
                    t = (cycle & 1) == 1 ? 1.0f - frac : frac;
                    break;

            }

            if (curveMode == CurveMode.Catmull_Rom)
                return base.Evaluate(t);
            else
            {
                if (t >= 1.0f)
                    return lastPosint;
                return EvaluateCubicHermite(t, moveCurves, vector2CurveHanldeDatas);
            }
        }


        public Vector2 EvaluateCubicHermite(float t, in Vector2[] moveCurves, in Vector2CurveHanldeData[] vector2CurveHanldeDatas)
        {


            int length = moveCurves.Length;
            int ratio = length - 1;

            float realValue = t * ratio;

            int index = (int)realValue;

            Vector2 p0 = moveCurves[index];
            Vector2 p1 = Clamp(vector2CurveHanldeDatas[index].outHandle) * testValue;
            Vector2 p2 = Clamp(vector2CurveHanldeDatas[index + 1].inHandle) * testValue;
            Vector2 p3 = moveCurves[index + 1];



            return CubicHermite(p0, p1, p2, p3, realValue - index);

        }


        public static Vector2 CubicHermite(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            float h00 = 2 * t3 - 3 * t2 + 1;
            float h10 = t3 - 2 * t2 + t;
            float h01 = -2 * t3 + 3 * t2;
            float h11 = t3 - t2;

            return h00 * p0 + h10 * p1 + h01 * p3 + h11 * p2;
        }


        private static Vector2 Clamp(Vector2 vector2)
        {
            vector2.x = Mathf.Clamp(vector2.x, -1.0f, 1.0f);
            vector2.y = Mathf.Clamp(vector2.y, -1.0f, 1.0f);

            return vector2;
        }

    }

}