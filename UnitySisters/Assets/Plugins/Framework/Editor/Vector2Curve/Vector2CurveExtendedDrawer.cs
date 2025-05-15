using System.Collections;
using System.Collections.Generic;

using UnityEditor;

using UnityEngine;

using UnityFramework;

using static UnityFramework.Vector2CurveExtended;

[CustomPropertyDrawer(typeof(Vector2CurveExtended), true)]
public class Vector2CurveExtendedDrawer : Vector2CurveDrawer
{
    private const string CURVE_PLAYE_MODE = "curvePlayeMode";
    private const string CURVE_MODE = "curveMode";
    private const string VECTOR2_CURVE_HANLDE_DATAS = "vector2CurveHanldeDatas";
    private CurveMode curveMode;

    private SerializedProperty hanldeData;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = base.GetPropertyHeight(property, label);
        if (property.isExpanded && curveMode == CurveMode.Hermite)
        {
            height += PropertyHeight(property, VECTOR2_CURVE_HANLDE_DATAS) + 20.0f;
        }
        return height;
    }

    protected override void DrawDot(in Rect rect, Vector2 graphPoint, SerializedProperty curveProp, int index)
    {
        base.DrawDot(rect, graphPoint, curveProp, index);

        if (index == selectedPointIndex)
        {
            var handle = hanldeData.GetArrayElementAtIndex(index);
            Vector2 outHandle = handle.FindPropertyRelative("outHandle").vector2Value;
            Vector2 inHandle = handle.FindPropertyRelative("inHandle").vector2Value;


            Vector2 outHandlePoint = graphPoint + outHandle * 10.0f;


            Vector2 inHandlePoint = graphPoint + inHandle * 10.0f;

            Handles.color = new Color(1f, 0.5f, 0f); 
            Handles.DrawWireDisc(outHandlePoint, Vector3.forward, 3.0f);
            Handles.color = Color.blue ;
            Handles.DrawWireDisc(inHandlePoint, Vector3.forward, 3.0f);
            Handles.color = Color.white;
            Handles.DrawLine(graphPoint, inHandlePoint);
            Handles.DrawLine(graphPoint, outHandlePoint);
        }

    }

    protected override void ChildPropertys(Rect position, SerializedProperty property, GUIContent label)
    {
        if (curveMode == CurveMode.Hermite)
        {
            hanldeData = property.FindPropertyRelative(VECTOR2_CURVE_HANLDE_DATAS);
            SerializedProperty curveProp = property.FindPropertyRelative(MOVEC_URVES);
            int curveSize = curveProp.arraySize;
            int handleSize = hanldeData.arraySize;
            if (curveSize != handleSize)
                hanldeData.arraySize = curveSize;

            EditorGUI.PropertyField(new Rect(position.x, base.GetPropertyHeight(property, null) - yValue + 30.0f, LIST_SIZE_X, GRAPH_SIZE), hanldeData, new GUIContent(VECTOR2_CURVE_HANLDE_DATAS));
        }
        SerializedProperty modeProp = property.FindPropertyRelative(CURVE_MODE);
        EditorGUILayout.PropertyField(modeProp);

        SerializedProperty curvePlayeMode = property.FindPropertyRelative(CURVE_PLAYE_MODE);
        EditorGUILayout.PropertyField(curvePlayeMode);

        curveMode = (CurveMode)modeProp.intValue;

        SerializedProperty testValue = property.FindPropertyRelative("testValue");
        EditorGUILayout.PropertyField(testValue);

    }

    protected override void AddCatmullRomSpline(in Rect rect, in List<Vector2> points, int i, List<Vector2> smoothPoints)
    {
        if (curveMode == CurveMode.Catmull_Rom)
        {
            base.AddCatmullRomSpline(rect, points, i, smoothPoints);
        }
        else
        {
            Vector2 p0 = points[i];
            Vector2 p3 = points[i + 1];
            Vector2 p1 = Vector2.zero;
            Vector2 p2 = Vector2.zero;
            if (hanldeData != null)
            {
                var inHandle = hanldeData.GetArrayElementAtIndex(i);
                p1 = inHandle.FindPropertyRelative("outHandle").vector2Value * rect.width;

                var outHandle = hanldeData.GetArrayElementAtIndex(i + 1);
                p2 = outHandle.FindPropertyRelative("inHandle").vector2Value * rect.width;
            }

            for (int j = 0; j < 100; j++)
            {
                float t = j * 0.01f;
                smoothPoints.Add(Vector2CurveExtended.CubicHermite(p0, p1, p2, p3, t));
            }
        }
    }
}
