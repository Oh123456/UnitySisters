using System.Collections;
using System.Collections.Generic;

using UnityEditor;

using UnityEngine;

using UnityFramework.UI;

[CustomEditor(typeof(UIBase), true)]
public class UIBaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        EditorGUILayout.Space(10.0f);

        if (GUILayout.Button("ToggleCanvas"))
        {
            Undo.RecordObject(target, $"{target.name} ToggleCanvas");
            UIBase ui = (UIBase)target;
            ui?.EditorActiveToggle();
            EditorUtility.SetDirty(target);
        }

        EditorGUILayout.Space(5.0f);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Show All Children"))
        {
            Undo.RecordObject(target, $"{target.name} Show All Children");
            UIBase ui = (UIBase)target;
            if (ui != null)
            {
                var bases = ui.transform.GetComponentsInChildren<UIBase>();
                foreach(var _base in bases)
                    _base.EditorShow(); 
            }
            EditorUtility.SetDirty(target);
        }

        if (GUILayout.Button("Hide All Children"))
        {
            Undo.RecordObject(target, $"{target.name} Hide All Children");
            UIBase ui = (UIBase)target;
            if (ui != null)
            {
                var bases = ui.transform.GetComponentsInChildren<UIBase>();
                foreach (var _base in bases)
                    _base.EditorHide();
            }
            EditorUtility.SetDirty(target);
        }
        EditorGUILayout.EndHorizontal();

    }
}
