using System.Collections;
using System.Collections.Generic;

using UnityEditor;

using UnityEngine;

using UnityFramework.UI;


[CustomEditor(typeof(SubUI), true)]
public class SubUIEditor : UIBaseEditor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        EditorGUILayout.Space(5.0f);
        if (GUILayout.Button("Reflash Parent"))
        {
            Undo.RecordObject(target, $"{target.name} Reflash Parent");
            SubUI sub = ((SubUI)(target));
            sub.FindParentUIBase();
            EditorUtility.SetDirty(target);
        }
    }
}
