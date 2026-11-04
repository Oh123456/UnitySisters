using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityFramework.BehaviorTree.Editor
{
    internal static class BlackboardEditorGUI
    {
        internal static string ValueField(BlackboardKeyType type)
        {
            switch (type)
            {
                case BlackboardKeyType.Bool: return "boolValue";
                case BlackboardKeyType.Int: return "intValue";
                case BlackboardKeyType.Float: return "floatValue";
                case BlackboardKeyType.Vector3: return "vectorValue";
                default: return "objectValue";
            }
        }

        internal static BlackboardKeyData DrawKey(Rect rect, BlackboardData schema, SerializedProperty id, Type filter = null)
        {
            List<GUIContent> names = new List<GUIContent> { new GUIContent("<None>") };
            List<string> ids = new List<string> { string.Empty };
            if (schema != null)
                foreach (BlackboardKeyData key in schema.Keys)
                    if (filter == null || BlackboardData.GetValueType(key.Type) == filter)
                    { names.Add(new GUIContent(key.Name)); ids.Add(key.ID); }
            int current = ids.IndexOf(id.stringValue ?? string.Empty);
            if (current < 0) { names.Add(new GUIContent("<Missing or incompatible key>")); ids.Add(id.stringValue); current = ids.Count - 1; }
            EditorGUI.BeginChangeCheck();
            int next = EditorGUI.Popup(rect, new GUIContent("Key"), current, names.ToArray());
            if (EditorGUI.EndChangeCheck()) id.stringValue = ids[next];
            return schema == null ? null : schema.FindKey(id.stringValue);
        }

        internal static void DrawDefinition(BlackboardData data, bool editable)
        {
            if (data == null) { EditorGUILayout.HelpBox("Assign a Blackboard asset.", MessageType.Info); return; }
            using (SerializedObject serialized = new SerializedObject(data))
            using (new EditorGUI.DisabledScope(!editable))
            {
                serialized.Update();
                SerializedProperty keys = serialized.FindProperty("keys");
                for (int index = 0; index < keys.arraySize; index++)
                {
                    SerializedProperty key = keys.GetArrayElementAtIndex(index);
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PropertyField(key.FindPropertyRelative("name"), GUIContent.none);
                    bool remove = GUILayout.Button("Remove", GUILayout.Width(65));
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.PropertyField(key.FindPropertyRelative("type"));
                    BlackboardKeyType kind = (BlackboardKeyType)key.FindPropertyRelative("type").enumValueIndex;
                    EditorGUILayout.PropertyField(key.FindPropertyRelative("defaultValue").FindPropertyRelative(ValueField(kind)), new GUIContent("Default"));
                    EditorGUILayout.EndVertical();
                    if (remove) { keys.DeleteArrayElementAtIndex(index); break; }
                }
                if (GUILayout.Button("Add key"))
                {
                    int index = keys.arraySize;
                    keys.InsertArrayElementAtIndex(index);
                    SerializedProperty key = keys.GetArrayElementAtIndex(index);
                    key.FindPropertyRelative("id").stringValue = Guid.NewGuid().ToString("N");
                    string name = "Key";
                    int suffix = 1;
                    while (HasName(keys, index, name)) name = "Key" + suffix++;
                    key.FindPropertyRelative("name").stringValue = name;
                    key.FindPropertyRelative("type").enumValueIndex = 0;
                    SerializedProperty value = key.FindPropertyRelative("defaultValue");
                    value.FindPropertyRelative("boolValue").boolValue = false;
                    value.FindPropertyRelative("intValue").intValue = 0;
                    value.FindPropertyRelative("floatValue").floatValue = 0;
                    value.FindPropertyRelative("vectorValue").vector3Value = Vector3.zero;
                    value.FindPropertyRelative("objectValue").objectReferenceValue = null;
                }
                if (editable && serialized.ApplyModifiedProperties()) EditorUtility.SetDirty(data);
            }
        }

        private static bool HasName(SerializedProperty keys, int excluded, string name)
        {
            for (int index = 0; index < keys.arraySize; index++)
                if (index != excluded && keys.GetArrayElementAtIndex(index).FindPropertyRelative("name").stringValue == name) return true;
            return false;
        }
    }

    [CustomEditor(typeof(BlackboardData))]
    internal sealed class BlackboardDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI() { BlackboardEditorGUI.DrawDefinition((BlackboardData)target, !EditorApplication.isPlayingOrWillChangePlaymode); }
    }

    [CustomEditor(typeof(BehaviorTreeData))]
    internal sealed class BehaviorTreeDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("Open Behavior Tree Editor")) BehaviorTreeEditorWindow.Open((BehaviorTreeData)target);
            EditorGUILayout.LabelField("Blackboard", ((BehaviorTreeData)target).BlackboardData == null ? "Not assigned" : ((BehaviorTreeData)target).BlackboardData.name);
        }
    }

    [CustomEditor(typeof(BehaviorTreeComponent))]
    internal sealed class BehaviorTreeComponentEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode)) DrawDefaultInspector();
            if (GUILayout.Button("Open Behavior Tree Editor")) BehaviorTreeEditorWindow.Open(null);
        }
    }

    [CustomPropertyDrawer(typeof(BlackboardKeyReference), true)]
    internal sealed class BlackboardKeyReferenceDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            BlackboardKeyReference reference = property.boxedValue as BlackboardKeyReference;
            BlackboardData schema = (property.serializedObject.targetObject as BehaviorTreeData)?.BlackboardData;
            EditorGUI.BeginProperty(position, label, property);
            BlackboardEditorGUI.DrawKey(position, schema, property.FindPropertyRelative("id"), reference?.ValueType);
            EditorGUI.EndProperty();
        }
    }

    [CustomPropertyDrawer(typeof(BlackboardDecorator))]
    internal sealed class BlackboardConditionDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) { return (EditorGUIUtility.singleLineHeight + 3) * 5; }
        public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            rect.height = height;
            EditorGUI.LabelField(rect, label, EditorStyles.boldLabel); rect.y += height + 3;
            EditorGUI.PropertyField(rect, property.FindPropertyRelative("abortMode")); rect.y += height + 3;
            BlackboardData schema = (property.serializedObject.targetObject as BehaviorTreeData)?.BlackboardData;
            BlackboardKeyData key = BlackboardEditorGUI.DrawKey(rect, schema, property.FindPropertyRelative("keyId")); rect.y += height + 3;
            EditorGUI.PropertyField(rect, property.FindPropertyRelative("comparison")); rect.y += height + 3;
            if (key != null) EditorGUI.PropertyField(rect, property.FindPropertyRelative("expected").FindPropertyRelative(BlackboardEditorGUI.ValueField(key.Type)), new GUIContent("Expected"));
        }
    }

    [CustomPropertyDrawer(typeof(SetBlackboardValueTaskData))]
    internal class SetBlackboardValueDrawer : PropertyDrawer
    {
        protected virtual bool HasInterval { get => false; }
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) { return (EditorGUIUtility.singleLineHeight + 3) * (HasInterval ? 4 : 3); }
        public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            rect.height = height;
            EditorGUI.LabelField(rect, label, EditorStyles.boldLabel); rect.y += height + 3;
            if (HasInterval) { EditorGUI.PropertyField(rect, property.FindPropertyRelative("interval")); rect.y += height + 3; }
            BlackboardData schema = (property.serializedObject.targetObject as BehaviorTreeData)?.BlackboardData;
            BlackboardKeyData key = BlackboardEditorGUI.DrawKey(rect, schema, property.FindPropertyRelative("keyId")); rect.y += height + 3;
            if (key != null) EditorGUI.PropertyField(rect, property.FindPropertyRelative("value").FindPropertyRelative(BlackboardEditorGUI.ValueField(key.Type)), new GUIContent("Value"));
        }
    }

    [CustomPropertyDrawer(typeof(SetBlackboardValueService))]
    internal sealed class SetBlackboardValueServiceDrawer : SetBlackboardValueDrawer
    {
        protected override bool HasInterval { get => true; }
    }
}
