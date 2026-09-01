using System.Collections.Generic;

using UnityEditor;
using UnityEngine;
using UnitySisters.Controller;

[CustomEditor(typeof(AttackController))]
public sealed class AttackControllerEditor : Editor
{
    private static readonly Dictionary<int, AttackDataScriptableObject> DebugDataByTarget = new Dictionary<int, AttackDataScriptableObject>();
    private static readonly Dictionary<int, bool> ShowHitboxByTarget = new Dictionary<int, bool>();

    private SerializedProperty hitboxStartProperty;

    static AttackControllerEditor()
    {
        SceneView.duringSceneGui -= DrawSelectedChildHitboxes;
        SceneView.duringSceneGui += DrawSelectedChildHitboxes;
        Selection.selectionChanged -= RepaintSceneViews;
        Selection.selectionChanged += RepaintSceneViews;
    }

    private void OnEnable()
    {
        hitboxStartProperty = serializedObject.FindProperty("hitboxStart");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(hitboxStartProperty);

        int targetId = target.GetInstanceID();
        bool showHitbox = ShowHitboxByTarget.TryGetValue(targetId, out bool storedShowHitbox) && storedShowHitbox;
        AttackDataScriptableObject debugData = DebugDataByTarget.TryGetValue(targetId, out AttackDataScriptableObject storedData) ? storedData : null;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Hitbox Debug", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        showHitbox = EditorGUILayout.Toggle("Show Hitbox", showHitbox);
        debugData = EditorGUILayout.ObjectField("Attack Data", debugData, typeof(AttackDataScriptableObject), false) as AttackDataScriptableObject;
        if (EditorGUI.EndChangeCheck())
        {
            ShowHitboxByTarget[targetId] = showHitbox;
            DebugDataByTarget[targetId] = debugData;
            SceneView.RepaintAll();
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void OnSceneGUI()
    {
        DrawDebugHitbox((AttackController)target, hitboxStartProperty.objectReferenceValue as Transform);
    }

    private static void DrawSelectedChildHitboxes(SceneView sceneView)
    {
        Transform[] selectedTransforms = Selection.transforms;
        HashSet<int> drawnControllerIds = new HashSet<int>();
        for (int i = 0; i < selectedTransforms.Length; i++)
        {
            Transform selectedTransform = selectedTransforms[i];
            if (selectedTransform == null)
            {
                continue;
            }

            AttackController controller = selectedTransform.GetComponentInParent<AttackController>();
            if (controller == null || controller.transform == selectedTransform)
            {
                continue;
            }

            if (!drawnControllerIds.Add(controller.GetInstanceID()))
            {
                continue;
            }

            DrawDebugHitbox(controller, GetHitboxStart(controller));
        }
    }

    private static void RepaintSceneViews()
    {
        SceneView.RepaintAll();
    }

    private static Transform GetHitboxStart(AttackController controller)
    {
        SerializedObject serializedController = new SerializedObject(controller);
        SerializedProperty hitboxStartProperty = serializedController.FindProperty("hitboxStart");
        return hitboxStartProperty?.objectReferenceValue as Transform;
    }

    private static void DrawDebugHitbox(AttackController controller, Transform hitboxStart)
    {
        if (controller == null)
        {
            return;
        }

        int targetId = controller.GetInstanceID();
        if (!ShowHitboxByTarget.TryGetValue(targetId, out bool showHitbox) || !showHitbox)
        {
            return;
        }

        if (!DebugDataByTarget.TryGetValue(targetId, out AttackDataScriptableObject data) || data == null)
        {
            return;
        }

        if (hitboxStart == null)
        {
            return;
        }

        Vector3 origin = hitboxStart.TransformPoint(data.Offset);
        Vector3 direction = hitboxStart.forward;
        Quaternion rotation = hitboxStart.rotation;
        float length = Mathf.Max(0.0f, data.Length);

        Handles.color = new Color(1.0f, 0.35f, 0.1f, 0.95f);
        switch (data.HitBoxType)
        {
            case HitBoxType.Line:
                DrawLineHitbox(origin, direction, length);
                break;

            case HitBoxType.Box:
                DrawBoxCastHitbox(origin, rotation, length, data.BoxSize);
                break;

            case HitBoxType.Sphere:
                DrawSphereCastHitbox(origin, direction, length, data.Radius);
                break;
        }
    }

    private static void DrawLineHitbox(Vector3 origin, Vector3 direction, float length)
    {
        Vector3 end = origin + direction * length;
        Handles.DrawAAPolyLine(3.0f, origin, end);
        Handles.SphereHandleCap(0, origin, Quaternion.identity, HandleUtility.GetHandleSize(origin) * 0.08f, EventType.Repaint);
        Handles.SphereHandleCap(0, end, Quaternion.identity, HandleUtility.GetHandleSize(end) * 0.08f, EventType.Repaint);
    }

    private static void DrawSphereCastHitbox(Vector3 origin, Vector3 direction, float length, float radius)
    {
        radius = Mathf.Max(0.0f, radius);
        Vector3 end = origin + direction * length;

        Handles.DrawWireDisc(origin, Vector3.up, radius);
        Handles.DrawWireDisc(origin, Vector3.right, radius);
        Handles.DrawWireDisc(origin, Vector3.forward, radius);
        Handles.DrawWireDisc(end, Vector3.up, radius);
        Handles.DrawWireDisc(end, Vector3.right, radius);
        Handles.DrawWireDisc(end, Vector3.forward, radius);

        Handles.DrawAAPolyLine(2.0f, origin + Vector3.up * radius, end + Vector3.up * radius);
        Handles.DrawAAPolyLine(2.0f, origin - Vector3.up * radius, end - Vector3.up * radius);
        Handles.DrawAAPolyLine(2.0f, origin + Vector3.right * radius, end + Vector3.right * radius);
        Handles.DrawAAPolyLine(2.0f, origin - Vector3.right * radius, end - Vector3.right * radius);
    }

    private static void DrawBoxCastHitbox(Vector3 origin, Quaternion rotation, float length, Vector3 halfExtents)
    {
        halfExtents = new Vector3(Mathf.Abs(halfExtents.x), Mathf.Abs(halfExtents.y), Mathf.Abs(halfExtents.z));
        Vector3 size = halfExtents * 2.0f;
        Matrix4x4 previousMatrix = Handles.matrix;
        try
        {
            Handles.matrix = Matrix4x4.TRS(origin, rotation, Vector3.one);

            Vector3 end = Vector3.forward * length;
            Handles.DrawWireCube(Vector3.zero, size);
            Handles.DrawWireCube(end, size);

            Vector3[] corners = GetBoxCorners(halfExtents);
            for (int i = 0; i < corners.Length; i++)
            {
                Handles.DrawAAPolyLine(2.0f, corners[i], end + corners[i]);
            }
        }
        finally
        {
            Handles.matrix = previousMatrix;
        }
    }

    private static Vector3[] GetBoxCorners(Vector3 halfExtents)
    {
        return new[]
        {
            new Vector3(-halfExtents.x, -halfExtents.y, -halfExtents.z),
            new Vector3(-halfExtents.x, -halfExtents.y, halfExtents.z),
            new Vector3(-halfExtents.x, halfExtents.y, -halfExtents.z),
            new Vector3(-halfExtents.x, halfExtents.y, halfExtents.z),
            new Vector3(halfExtents.x, -halfExtents.y, -halfExtents.z),
            new Vector3(halfExtents.x, -halfExtents.y, halfExtents.z),
            new Vector3(halfExtents.x, halfExtents.y, -halfExtents.z),
            new Vector3(halfExtents.x, halfExtents.y, halfExtents.z),
        };
    }
}
