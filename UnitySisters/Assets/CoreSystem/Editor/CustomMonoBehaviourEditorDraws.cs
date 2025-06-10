namespace CoreSystem.Editor
{
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    public partial class CustomMonoBehaviourEditor
    {
        private static Dictionary<System.Type, System.Action<string, object>> darwFields = new Dictionary<System.Type, System.Action<string, object>>()
        {
            //Vector
            { typeof(Vector2) , (label , value) => value = EditorGUILayout.Vector2Field(label, (Vector2)value)},
            { typeof(Vector2Int) , (label , value) => value = EditorGUILayout.Vector2IntField(label, (Vector2Int)value)},
            { typeof(Vector3) , (label , value) => value = EditorGUILayout.Vector3Field(label, (Vector3)value)},
            { typeof(Vector3Int) , (label , value) => value = EditorGUILayout.Vector3IntField(label, (Vector3Int)value)},

            // values
            { typeof(int) , (label , value) => value = EditorGUILayout.IntField(label, (int)value)},
            { typeof(float) , (label , value) => value = EditorGUILayout.FloatField(label, (float)value)},
            { typeof(bool) , (label , value) => value = EditorGUILayout.Toggle(label, (bool)value)},


        };
    }
}