using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

using UnityEngine;

namespace UnityFramework.JobSystem
{

    public static class Vector2CurveJob
    {
        public static float2 CatmullRom(this Vector2Curve Vector2, float2 p0, float2 p1, float2 p2, float2 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            return 0.5f * (
                (2 * p1) +
                (-p0 + p2) * t +
                (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 +
                (-p0 + 3 * p1 - 3 * p2 + p3) * t3
            );
        }

        public static Vector2CurveJobData ToVector2CurveJobData(this Vector2Curve vector2Curve, Allocator allocator)
        {
            return new Vector2CurveJobData()
            {
                moveCurves = new NativeArray<Vector2>(vector2Curve.MoveCurves, allocator),
            };
        }

    }

    [BurstCompile]
    public struct Vector2CurveJobData : System.IDisposable
    {
        public NativeArray<Vector2> moveCurves;

        public Vector2 Evaluate(float t)
        {
            t = Mathf.Clamp01(t);

            int ratio = moveCurves.Length - 1;

            float realValue = t * ratio;

            int index = (int)realValue;

            Vector2 p0 = (index == 0) ? moveCurves[index] : moveCurves[index - 1];
            Vector2 p1 = moveCurves[index];
            Vector2 p2 = moveCurves[index + 1];
            Vector2 p3 = (index == moveCurves.Length - 2) ? moveCurves[index + 1] : moveCurves[index + 2];


            return Vector2Curve.CatmullRom(p0, p1, p2, p3, realValue % 1.0f);
        }

        public void Dispose()
        {
            if (moveCurves.IsCreated)
                moveCurves.Dispose();
        }
    }
}