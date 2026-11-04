using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityFramework.BehaviorTree
{
    [Serializable]
    internal sealed class BlackboardValue
    {
        [SerializeField] private bool boolValue;
        internal bool BoolValue { get => boolValue; set => boolValue = value; }
        [SerializeField] private int intValue;
        internal int IntValue { get => intValue; set => intValue = value; }
        [SerializeField] private float floatValue;
        internal float FloatValue { get => floatValue; set => floatValue = value; }
        [SerializeField] private Vector3 vectorValue;
        internal Vector3 VectorValue { get => vectorValue; set => vectorValue = value; }
        [SerializeField] private UnityEngine.Object objectValue;
        internal UnityEngine.Object ObjectValue { get => objectValue; set => objectValue = value; }
    }

    [Serializable]
    internal sealed class BlackboardKeyData
    {
        [SerializeField] private string id;
        internal string ID { get => id; set => id = value; }
        [SerializeField] private string name;
        internal string Name { get => name; set => name = value; }
        [SerializeField] private BlackboardKeyType type;
        internal BlackboardKeyType Type { get => type; set => type = value; }
        [SerializeField] private BlackboardValue defaultValue = new BlackboardValue();
        internal BlackboardValue DefaultValue { get => defaultValue; set => defaultValue = value; }
    }

    [CreateAssetMenu(menuName = "UnityFramework/Behavior Tree/Blackboard")]
    public sealed class BlackboardData : ScriptableObject
    {
        [SerializeField] private List<BlackboardKeyData> keys = new List<BlackboardKeyData>();
        internal IReadOnlyList<BlackboardKeyData> Keys { get => keys; }

        public BlackboardKeyReference<T> GetKey<T>(string keyName)
        {
            foreach (BlackboardKeyData key in keys)
                if (key.Name == keyName)
                {
                    if (GetValueType(key.Type) != typeof(T))
                        throw new ArgumentException($"Blackboard key '{keyName}' has type {key.Type}, not {typeof(T).Name}.");
                    return new BlackboardKeyReference<T>(key.ID);
                }
            throw new KeyNotFoundException($"Blackboard key '{keyName}' was not found.");
        }

        public bool ContainsKey(BlackboardKeyReference key)
        {
            BlackboardKeyData data = FindKey(key?.ID);
            return data != null && GetValueType(data.Type) == key.ValueType;
        }

        internal BlackboardKeyData FindKey(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (BlackboardKeyData key in keys)
                if (key.ID == id) return key;
            return null;
        }

        internal static Type GetValueType(BlackboardKeyType type)
        {
            switch (type)
            {
                case BlackboardKeyType.Bool: return typeof(bool);
                case BlackboardKeyType.Int: return typeof(int);
                case BlackboardKeyType.Float: return typeof(float);
                case BlackboardKeyType.Vector3: return typeof(Vector3);
                case BlackboardKeyType.Object: return typeof(UnityEngine.Object);
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
        }

        internal void Validate(List<string> errors)
        {
            HashSet<string> ids = new HashSet<string>();
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            foreach (BlackboardKeyData key in keys)
            {
                if (key == null) { errors.Add("Blackboard contains a missing key definition."); continue; }
                if (string.IsNullOrEmpty(key.ID) || !ids.Add(key.ID)) errors.Add("Blackboard key IDs must be present and unique.");
                if (string.IsNullOrWhiteSpace(key.Name) || !names.Add(key.Name)) errors.Add("Blackboard key names must be present and unique.");
                if (!Enum.IsDefined(typeof(BlackboardKeyType), key.Type) || key.DefaultValue == null) errors.Add($"Invalid definition for key '{key.Name}'.");
            }
        }
    }
}
