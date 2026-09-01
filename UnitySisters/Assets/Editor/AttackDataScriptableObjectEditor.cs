using UnityEditor;

[CustomEditor(typeof(AttackDataScriptableObject))]
public sealed class AttackDataScriptableObjectEditor : Editor
{
    private SerializedProperty keyProperty;
    private SerializedProperty offsetProperty;
    private SerializedProperty distanceProperty;
    private SerializedProperty hitBoxTypeProperty;
    private SerializedProperty layerMaskProperty;
    private SerializedProperty radiusProperty;
    private SerializedProperty boxSizeProperty;
    private SerializedProperty lengthProperty;

    private void OnEnable()
    {
        keyProperty = serializedObject.FindProperty("key");
        offsetProperty = serializedObject.FindProperty("offset");
        distanceProperty = serializedObject.FindProperty("distance");
        hitBoxTypeProperty = serializedObject.FindProperty("hitBoxType");
        layerMaskProperty = serializedObject.FindProperty("layerMask");
        radiusProperty = serializedObject.FindProperty("radius");
        boxSizeProperty = serializedObject.FindProperty("boxSize");
        lengthProperty = serializedObject.FindProperty("length");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawProperty(keyProperty);
        DrawProperty(offsetProperty);
        DrawProperty(distanceProperty);
        DrawProperty(hitBoxTypeProperty);
        DrawProperty(layerMaskProperty);
        DrawProperty(lengthProperty);

        if (hitBoxTypeProperty == null)
        {
            serializedObject.ApplyModifiedProperties();
            return;
        }

        HitBoxType hitBoxType = (HitBoxType)hitBoxTypeProperty.enumValueIndex;
        switch (hitBoxType)
        {
            case HitBoxType.Line:
                break;

            case HitBoxType.Box:
                DrawProperty(boxSizeProperty);
                break;

            case HitBoxType.Sphere:
                DrawProperty(radiusProperty);
                break;
        }

        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawProperty(SerializedProperty property)
    {
        if (property != null)
        {
            EditorGUILayout.PropertyField(property);
        }
    }
}
