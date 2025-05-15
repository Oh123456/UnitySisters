#if UNITY_EDITOR
using System.Collections.Generic;

using UnityEditor;

using UnityEngine;

using UnityFramework;



[CustomPropertyDrawer(typeof(Vector2Curve), true)]
public class Vector2CurveDrawer : PropertyDrawer
{
    private const int BUTTON_SIZE = 100;
    protected const int GRAPH_SIZE = 300; // 洹몃옒???ш린 (px)
    protected const int GRAPH_SIZE_X = 500; // 洹몃옒???ш린 (px)
    protected const int LIST_SIZE_X = GRAPH_SIZE_X + 50;
    protected const string MOVEC_URVES = "moveCurves";
    private const string ADD_POINT = "Add Point";
    private GUIContent addPointGUIContent = new GUIContent(ADD_POINT);

    protected const float xValue = 20.0f;
    protected const float yValue = 30.0f;
    protected int selectedPointIndex = -1; // ?좏깮???먯쓽 ?몃뜳??
    private Vector2 lastMousePos;
    private List<Vector2> smoothPoints = new List<Vector2>();

    private bool showRealCure = false;



    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        GUIContent gUIContent = new GUIContent();

        // ?쇰꺼怨?UI 洹몃━湲?
        Rect foldoutRect = new Rect(position.x + 10.0f, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label);

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            // color ?꾨뱶
            Rect graphRect = new Rect(position.x + xValue, position.y + yValue, GRAPH_SIZE, GRAPH_SIZE);
            GUI.Box(graphRect, ""); // 洹몃옒??諛곌꼍
            GUI.Label(new Rect(position.x + xValue + 10.0f + GRAPH_SIZE, position.y + GRAPH_SIZE - 30.0f, GRAPH_SIZE, BUTTON_SIZE), $"SelectedPointIndex : {selectedPointIndex}");
            SerializedProperty curveProp = property.FindPropertyRelative(MOVEC_URVES);
            Rect posRect = new Rect(position.x, position.y + yValue + 10.0f, position.width, EditorGUIUtility.singleLineHeight);
            DrawGraph(graphRect, curveProp, property); // 洹몃옒??洹몃━湲?
            EditorGUI.PropertyField(new Rect(position.x, position.y + yValue + 20.0f + GRAPH_SIZE, LIST_SIZE_X, GRAPH_SIZE), curveProp, new GUIContent(MOVEC_URVES));
            ChildPropertys(position, property, label);

            EditorGUI.indentLevel--;
        }



        EditorGUI.EndProperty();
    }

    protected virtual void DrawGraph(Rect rect, SerializedProperty curveProp, SerializedProperty target)
    {
        if (curveProp.arraySize < 2) return; // 理쒖냼????媛쒖쓽 ?먯씠 ?덉뼱???좎쓣 洹몃┝

        Handles.color = Color.black; // ???됱긽 ?ㅼ젙
        List<Vector2> points = new List<Vector2>();

        Event e = Event.current;
        lastMousePos = e.mousePosition;

        float left = rect.x;
        float right = rect.x + rect.width;
        float top = rect.y;
        float bottom = rect.y + rect.height;
        Vector2 zeroPoint = new Vector2(rect.x + rect.width * 0.5f, rect.y + rect.height * 0.5f);

        Handles.DrawLine(new Vector2(left, zeroPoint.y), new Vector2(right, zeroPoint.y));
        Handles.DrawLine(new Vector2(zeroPoint.x, top), new Vector2(zeroPoint.x, bottom));

        for (int i = 0; i < curveProp.arraySize; i++)
        {
            SerializedProperty pointProp = curveProp.GetArrayElementAtIndex(i);
            Vector2 point = pointProp.vector2Value;


            // 그래프를 정규화 
            Vector2 graphPoint = new Vector2(
                Mathf.Lerp(rect.x, rect.x + rect.width, (point.x + 1) * 0.5f),
                Mathf.Lerp(rect.y + rect.height, rect.y, (point.y + 1) * 0.5f)
            );

            points.Add(graphPoint);

            // ?먯쓣 ?좏깮 (留덉슦???대┃ 媛먯?)
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (Vector2.Distance(lastMousePos, graphPoint) < 10f) // 버튼 클릭 범위
                {
                    selectedPointIndex = i;
                    e.Use();
                }
                else if (rect.Contains(lastMousePos))
                {
                    selectedPointIndex = -1;
                }

            }

            if (rect.Contains(lastMousePos) && selectedPointIndex == i && e.type == EventType.MouseDrag)
            {
                Vector2 newPoint = new Vector2(
                    Mathf.InverseLerp(rect.x, rect.x + rect.width, lastMousePos.x) * 2 - 1,
                    Mathf.InverseLerp(rect.y + rect.height, rect.y, lastMousePos.y) * 2 - 1
                );

                pointProp.vector2Value = newPoint;
                e.Use();
            }

            DrawDot(in rect, graphPoint, curveProp, i);

            if (rect.Contains(e.mousePosition) && e.type == EventType.ContextClick) // 마우스 클릭
            {
                GenericMenu menu = new GenericMenu();
                menu.AddItem(addPointGUIContent, false, () => AddPoint(curveProp, rect));
                menu.ShowAsContext();
                e.Use();
            }

        }

        Handles.color = Color.green;
        // 선그리기

        var list = GenerateCatmullRomSpline(rect, points);

        for (int i = 0; i < list.Count - 1; i++)
        {
            Handles.DrawLine(list[i], list[i + 1]);
        }

        Handles.color = Color.red;
        if (this.showRealCure)
        {

            Vector2[] vector2s = new Vector2[curveProp.arraySize];

            for (int i = 0; i < curveProp.arraySize; i++)
            {
                Vector2 v = curveProp.GetArrayElementAtIndex(i).vector2Value;
                vector2s[i] = new Vector2(
                                          Mathf.Lerp(rect.x, rect.x + rect.width, (v.x + 1) * 0.5f),
                                          Mathf.Lerp(rect.y + rect.height, rect.y, (v.y + 1) * 0.5f));
            }


            Vector2 point = Vector2Curve.EvaluateCatmullRom(0, vector2s);
            for (int i = 1; i < 100; i++)
            {
                Vector2 current = Vector2Curve.EvaluateCatmullRom(i * 0.01f, vector2s);
                Handles.DrawLine(point, current);
                point = current;
            }

        }

    }

    protected virtual void DrawDot(in Rect rect, Vector2 graphPoint, SerializedProperty curveProp, int index)
    {
        if (selectedPointIndex == index)
            Handles.color = Color.red;
        else if (index == 0)
            Handles.color = Color.blue;
        else if (index == curveProp.arraySize - 1)
            Handles.color = Color.white;
        else
            Handles.color = Color.gray; // ???됱긽 ?ㅼ젙

        if (Handles.Button(graphPoint, Quaternion.identity, 5, 10, Handles.CircleHandleCap))
        {
            selectedPointIndex = index;
        }

    }

    protected virtual void ChildPropertys(Rect position, SerializedProperty property, GUIContent label)
    {

    }

    void AddPoint(SerializedProperty property, Rect graphRect)
    {
        Undo.RecordObject(property.serializedObject.targetObject, ADD_POINT);

        // 그래프를 정규화
        Vector2 graphMousePos = new Vector2(
            Mathf.InverseLerp(graphRect.x, graphRect.x + GRAPH_SIZE, lastMousePos.x) * 2 - 1,
            Mathf.InverseLerp(graphRect.y + GRAPH_SIZE, graphRect.y, lastMousePos.y) * 2 - 1
        );

        // 媛??媛源뚯슫 ????李얘린
        int insertIndex = 0;
        float closestDist = float.MaxValue;

        for (int i = 0; i < property.arraySize - 1; i++)
        {
            SerializedProperty p1 = property.GetArrayElementAtIndex(i);
            SerializedProperty p2 = property.GetArrayElementAtIndex(i + 1);
            Vector2 v1 = p1.vector2Value;
            Vector2 v2 = p2.vector2Value;

            // 留덉슦???꾩튂媛 ???먯쓣 ?뉖뒗 ?좊텇怨??쇰쭏??媛源뚯슫吏 ?뺤씤
            Vector2 projectedPoint = ClosestPointOnLineSegment(v1, v2, graphMousePos);
            float dist = Vector2.Distance(graphMousePos, projectedPoint);

            if (dist < closestDist)
            {
                closestDist = dist;
                insertIndex = i + 1; // ?꾩옱 ?꾩튂 ?ㅼ쓬???쎌엯
            }
        }

        // 由ъ뒪?몄뿉 ?????쎌엯
        property.InsertArrayElementAtIndex(insertIndex);
        property.GetArrayElementAtIndex(insertIndex).vector2Value = graphMousePos;

        property.serializedObject.ApplyModifiedProperties();

    }

    protected virtual void AddCatmullRomSpline(in Rect rect, in List<Vector2> points, int i, List<Vector2> smoothPoints)
    {

        Vector2 p0 = (i == 0) ? points[i] : points[i - 1];
        Vector2 p1 = points[i];
        Vector2 p2 = points[i + 1];
        Vector2 p3 = (i == points.Count - 2) ? points[i + 1] : points[i + 2];
        // 세그먼트
        for (int j = 0; j < 100; j++)
        {
            float t = j * 0.01f;// (float)resolution;                
            smoothPoints.Add(Vector2Curve.CatmullRom(p0, p1, p2, p3, t));
        }
    }

    private List<Vector2> GenerateCatmullRomSpline(in Rect rect, List<Vector2> points /*int resolution*/)
    {
        smoothPoints.Clear();
        for (int i = 0; i < points.Count - 1; i++)
        {
            AddCatmullRomSpline(rect, points, i, smoothPoints);
        }
        return smoothPoints;
    }

    private Vector2 ClosestPointOnLineSegment(Vector2 A, Vector2 B, Vector2 P)
    {
        Vector2 AP = P - A;
        Vector2 AB = B - A;
        float magnitudeAB = AB.sqrMagnitude;
        float ABAPproduct = Vector2.Dot(AP, AB);
        float distance = ABAPproduct / magnitudeAB;

        if (distance < 0) return A;
        if (distance > 1) return B;

        return A + AB * distance;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight; // 湲곕낯 ?믪씠
        if (property.isExpanded)
        {

            height += PropertyHeight(property, MOVEC_URVES);
            height += GRAPH_SIZE + 20; // 洹몃옒???믪씠 異붽?

        }
        return height + yValue;
    }

    protected float PropertyHeight(SerializedProperty property, string propertyName)
    {
        SerializedProperty pointsProp = property.FindPropertyRelative(propertyName);

        return EditorGUI.GetPropertyHeight(pointsProp, true);
    }
}

#endif