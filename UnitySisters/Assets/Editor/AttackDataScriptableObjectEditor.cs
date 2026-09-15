using UnityEditor;

[CustomEditor(typeof(AttackDataScriptableObject))]
public sealed class AttackDataScriptableObjectEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty hitBoxTypeProperty = serializedObject.FindProperty("hitBoxType");
        SerializedProperty property = serializedObject.GetIterator();
        bool enterChildren = true;
        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (!ShouldDrawProperty(property, hitBoxTypeProperty))
            {
                continue;
            }

            using (new EditorGUI.DisabledScope(property.propertyPath == "m_Script"))
            {
                EditorGUILayout.PropertyField(property, true);
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private static bool ShouldDrawProperty(SerializedProperty property, SerializedProperty hitBoxTypeProperty)
    {
        if (hitBoxTypeProperty == null)
        {
            return true;
        }

        HitBoxType hitBoxType = (HitBoxType)hitBoxTypeProperty.intValue;
        switch (property.propertyPath)
        {
            case "boxSize":
                return hitBoxType == HitBoxType.Box;
            case "radius":
                return hitBoxType == HitBoxType.Sphere;
            default:
                return true;
        }
    }
}
