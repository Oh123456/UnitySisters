using System;
using System.Collections.Generic;

using UnityEditor;
using UnityEngine;

namespace UnityFramework.BattleSystem.Editor
{
    [Serializable]
    internal sealed class BuffMonitorCachedField
    {
        [SerializeField] private string name;
        [SerializeField] private string value;
        [SerializeField] private int depth;

        internal string Name => name;
        internal string Value => value;
        internal int Depth => depth;

        internal BuffMonitorCachedField(string name, string value, int depth)
        {
            this.name = name;
            this.value = value;
            this.depth = depth;
        }
    }

    [Serializable]
    internal sealed class BuffMonitorCachedBuff
    {
        [SerializeField] private int buffId;
        [SerializeField] private int stack;
        [SerializeField] private int maxStack;
        [SerializeField] private string dataTypeName;
        [SerializeField] private List<BuffMonitorCachedField> dataFields = new();
        [SerializeField] private List<string> modifierNames = new();

        internal int BuffId => buffId;
        internal int Stack => stack;
        internal int MaxStack => maxStack;
        internal string DataTypeName => dataTypeName;
        internal IReadOnlyList<BuffMonitorCachedField> DataFields => dataFields;
        internal IReadOnlyList<string> ModifierNames => modifierNames;

        internal BuffMonitorCachedBuff(BuffMonitorSnapshot snapshot)
        {
            buffId = snapshot.BuffId;
            stack = snapshot.Stack;
            maxStack = snapshot.MaxStack;
            dataTypeName = snapshot.BuffData == null ? "Null" : snapshot.BuffData.GetType().Name;
            modifierNames.AddRange(snapshot.ModifierNames);
            BuffMonitorSessionCache.CaptureManagedReference(snapshot.BuffData, dataFields);
        }
    }

    [Serializable]
    internal sealed class BuffMonitorCachedComponent
    {
        [SerializeField] private string name;
        [SerializeField] private string hierarchyPath;
        [SerializeField] private List<BuffMonitorCachedField> attributeFields = new();
        [SerializeField] private List<BuffMonitorCachedBuff> buffs = new();

        internal string Name => name;
        internal string HierarchyPath => hierarchyPath;
        internal IReadOnlyList<BuffMonitorCachedField> AttributeFields => attributeFields;
        internal IReadOnlyList<BuffMonitorCachedBuff> Buffs => buffs;

        internal BuffMonitorCachedComponent(BattleComponent component, string path)
        {
            name = component.gameObject.name;
            hierarchyPath = path;

            SerializedObject componentObject = new SerializedObject(component);
            componentObject.UpdateIfRequiredOrScript();
            SerializedProperty attributeProperty = componentObject.FindProperty("battleAttributeSet");
            BuffMonitorSessionCache.CaptureProperty(attributeProperty, attributeFields);

            List<BuffMonitorSnapshot> snapshots = new List<BuffMonitorSnapshot>();
            if (!BuffMonitorReflection.TryCapture(component, snapshots, out _))
                return;

            for (int i = 0; i < snapshots.Count; i++)
                buffs.Add(new BuffMonitorCachedBuff(snapshots[i]));
        }
    }

    [FilePath(
        "Library/UnityFrameworkBuffMonitorSessionCache.asset",
        FilePathAttribute.Location.ProjectFolder)]
    internal sealed class BuffMonitorSessionCache : ScriptableSingleton<BuffMonitorSessionCache>
    {
        [SerializeField] private List<BuffMonitorCachedComponent> components = new();

        internal IReadOnlyList<BuffMonitorCachedComponent> Components => components;
        internal bool HasData => components.Count > 0;

        internal void Capture(IReadOnlyList<BattleComponent> battleComponents)
        {
            components.Clear();
            for (int i = 0; i < battleComponents.Count; i++)
            {
                BattleComponent component = battleComponents[i];
                if (component == null)
                    continue;

                components.Add(new BuffMonitorCachedComponent(
                    component,
                    GetHierarchyPath(component.transform)));
            }

            Save(true);
        }

        internal void ClearCache()
        {
            components.Clear();
            Save(true);
        }

        internal static void CaptureManagedReference(
            BuffData buffData,
            List<BuffMonitorCachedField> destination)
        {
            destination.Clear();
            if (buffData == null)
                return;

            BuffDataProxy proxy = CreateInstance<BuffDataProxy>();
            proxy.hideFlags = HideFlags.HideAndDontSave;
            proxy.SetData(buffData);
            SerializedObject serializedObject = new SerializedObject(proxy);
            CaptureProperty(serializedObject.FindProperty("data"), destination);
            DestroyImmediate(proxy);
        }

        internal static void CaptureProperty(
            SerializedProperty root,
            List<BuffMonitorCachedField> destination)
        {
            destination.Clear();
            if (root == null)
                return;

            SerializedProperty property = root.Copy();
            SerializedProperty end = property.GetEndProperty();
            int rootDepth = property.depth;
            bool enterChildren = true;
            while (property.NextVisible(enterChildren) &&
                   !SerializedProperty.EqualContents(property, end))
            {
                destination.Add(new BuffMonitorCachedField(
                    property.displayName,
                    GetPropertyValue(property),
                    Mathf.Max(0, property.depth - rootDepth - 1)));
                enterChildren = true;
            }
        }

        private static string GetPropertyValue(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return property.longValue.ToString();
                case SerializedPropertyType.Boolean:
                    return property.boolValue.ToString();
                case SerializedPropertyType.Float:
                    return property.doubleValue.ToString("G");
                case SerializedPropertyType.String:
                    return property.stringValue;
                case SerializedPropertyType.Color:
                    return property.colorValue.ToString();
                case SerializedPropertyType.ObjectReference:
                    return property.objectReferenceValue == null
                        ? "Null"
                        : property.objectReferenceValue.name;
                case SerializedPropertyType.Enum:
                    return property.enumValueIndex >= 0 &&
                           property.enumValueIndex < property.enumDisplayNames.Length
                        ? property.enumDisplayNames[property.enumValueIndex]
                        : property.enumValueIndex.ToString();
                case SerializedPropertyType.Vector2:
                    return property.vector2Value.ToString();
                case SerializedPropertyType.Vector3:
                    return property.vector3Value.ToString();
                case SerializedPropertyType.Vector4:
                    return property.vector4Value.ToString();
                case SerializedPropertyType.Rect:
                    return property.rectValue.ToString();
                case SerializedPropertyType.Bounds:
                    return property.boundsValue.ToString();
                case SerializedPropertyType.Quaternion:
                    return property.quaternionValue.eulerAngles.ToString();
                case SerializedPropertyType.Vector2Int:
                    return property.vector2IntValue.ToString();
                case SerializedPropertyType.Vector3Int:
                    return property.vector3IntValue.ToString();
                case SerializedPropertyType.RectInt:
                    return property.rectIntValue.ToString();
                case SerializedPropertyType.BoundsInt:
                    return property.boundsIntValue.ToString();
                case SerializedPropertyType.ManagedReference:
                    return property.managedReferenceValue == null
                        ? "Null"
                        : property.managedReferenceValue.GetType().Name;
                case SerializedPropertyType.ArraySize:
                    return property.intValue.ToString();
                default:
                    return string.Empty;
            }
        }

        private static string GetHierarchyPath(Transform transform)
        {
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = $"{transform.name}/{path}";
            }

            return path;
        }

        private sealed class BuffDataProxy : ScriptableObject
        {
            [SerializeReference] private BuffData data;

            internal void SetData(BuffData value)
            {
                data = value;
            }
        }
    }
}
