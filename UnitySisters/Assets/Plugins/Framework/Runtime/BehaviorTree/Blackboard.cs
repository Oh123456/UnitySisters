using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityFramework.BehaviorTree
{
    internal abstract class BlackboardSlot
    {
        internal abstract Type ValueType { get; }
        internal abstract object BoxedValue { get; }
        internal abstract bool Reset();
    }

    internal sealed class BlackboardSlot<T> : BlackboardSlot
    {
        private readonly T defaultValue;
        private T value;
        internal T Value { get => value; }
        internal override Type ValueType { get => typeof(T); }
        internal override object BoxedValue { get => value; }
        internal BlackboardSlot(T value) { this.value = defaultValue = value; }
        internal bool Set(T next)
        {
            if (EqualityComparer<T>.Default.Equals(value, next)) return false;
            value = next;
            return true;
        }
        internal override bool Reset() { return Set(defaultValue); }
    }

    internal sealed class Blackboard : IReadOnlyBlackboard, IBlackboardWriter
    {
        private readonly Dictionary<string, BlackboardSlot> slots = new Dictionary<string, BlackboardSlot>();
        private event Action<string> keyChanged;
        event Action<string> IReadOnlyBlackboard.KeyChanged { add => keyChanged += value; remove => keyChanged -= value; }

        internal Blackboard(BlackboardData data)
        {
            foreach (BlackboardKeyData key in data.Keys)
            {
                BlackboardSlot slot;
                switch (key.Type)
                {
                    case BlackboardKeyType.Bool: slot = new BlackboardSlot<bool>(key.DefaultValue.BoolValue); break;
                    case BlackboardKeyType.Int: slot = new BlackboardSlot<int>(key.DefaultValue.IntValue); break;
                    case BlackboardKeyType.Float: slot = new BlackboardSlot<float>(key.DefaultValue.FloatValue); break;
                    case BlackboardKeyType.Vector3: slot = new BlackboardSlot<Vector3>(key.DefaultValue.VectorValue); break;
                    case BlackboardKeyType.Object: slot = new BlackboardSlot<UnityEngine.Object>(key.DefaultValue.ObjectValue); break;
                    default: throw new ArgumentOutOfRangeException();
                }
                slots.Add(key.ID, slot);
            }
        }

        T IReadOnlyBlackboard.GetValue<T>(BlackboardKeyReference<T> key)
        {
            if (!TryRead(key, out T value)) throw new ArgumentException("Missing blackboard key or incompatible value type.", nameof(key));
            return value;
        }

        bool IReadOnlyBlackboard.TryGetValue<T>(BlackboardKeyReference<T> key, out T value) { return TryRead(key, out value); }

        private bool TryRead<T>(BlackboardKeyReference<T> key, out T value)
        {
            if (key != null && slots.TryGetValue(key.ID ?? string.Empty, out BlackboardSlot slot) && slot is BlackboardSlot<T> typed)
            { value = typed.Value; return true; }
            value = default;
            return false;
        }

        void IBlackboardWriter.SetValue<T>(BlackboardKeyReference<T> key, T value)
        {
            if (key == null || !slots.TryGetValue(key.ID ?? string.Empty, out BlackboardSlot slot) || !(slot is BlackboardSlot<T> typed))
                throw new ArgumentException("Missing blackboard key or incompatible value type.", nameof(key));
            if (typed.Set(value)) keyChanged?.Invoke(key.ID);
        }

        void IBlackboardWriter.ResetValue(BlackboardKeyReference key)
        {
            if (key == null || !slots.TryGetValue(key.ID ?? string.Empty, out BlackboardSlot slot) || slot.ValueType != key.ValueType) throw new ArgumentException("Missing blackboard key or incompatible value type.", nameof(key));
            if (slot.Reset()) keyChanged?.Invoke(key.ID);
        }

        internal object ReadBoxed(string id) { return slots.TryGetValue(id, out BlackboardSlot slot) ? slot.BoxedValue : null; }

        internal void Write(string id, BlackboardKeyType type, BlackboardValue value)
        {
            bool changed;
            switch (type)
            {
                case BlackboardKeyType.Bool: changed = ((BlackboardSlot<bool>)slots[id]).Set(value.BoolValue); break;
                case BlackboardKeyType.Int: changed = ((BlackboardSlot<int>)slots[id]).Set(value.IntValue); break;
                case BlackboardKeyType.Float: changed = ((BlackboardSlot<float>)slots[id]).Set(value.FloatValue); break;
                case BlackboardKeyType.Vector3: changed = ((BlackboardSlot<Vector3>)slots[id]).Set(value.VectorValue); break;
                case BlackboardKeyType.Object: changed = ((BlackboardSlot<UnityEngine.Object>)slots[id]).Set(value.ObjectValue); break;
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
            if (changed) keyChanged?.Invoke(id);
        }

        internal bool Compare(string id, BlackboardKeyType type, BlackboardComparison comparison, BlackboardValue expected)
        {
            switch (type)
            {
                case BlackboardKeyType.Bool: return CompareOrder(((BlackboardSlot<bool>)slots[id]).Value.CompareTo(expected.BoolValue), comparison);
                case BlackboardKeyType.Int: return CompareOrder(((BlackboardSlot<int>)slots[id]).Value.CompareTo(expected.IntValue), comparison);
                case BlackboardKeyType.Float: return CompareOrder(((BlackboardSlot<float>)slots[id]).Value.CompareTo(expected.FloatValue), comparison);
                case BlackboardKeyType.Vector3: return CompareOrder(((BlackboardSlot<Vector3>)slots[id]).Value.Equals(expected.VectorValue) ? 0 : 1, comparison);
                case BlackboardKeyType.Object:
                    UnityEngine.Object value = ((BlackboardSlot<UnityEngine.Object>)slots[id]).Value;
                    if (comparison == BlackboardComparison.IsSet) return value != null;
                    if (comparison == BlackboardComparison.IsNotSet) return value == null;
                    return CompareOrder(value == expected.ObjectValue ? 0 : 1, comparison);
                default: return false;
            }
        }

        private static bool CompareOrder(int order, BlackboardComparison comparison)
        {
            switch (comparison)
            {
                case BlackboardComparison.Equal: return order == 0;
                case BlackboardComparison.NotEqual: return order != 0;
                case BlackboardComparison.Less: return order < 0;
                case BlackboardComparison.LessOrEqual: return order <= 0;
                case BlackboardComparison.Greater: return order > 0;
                case BlackboardComparison.GreaterOrEqual: return order >= 0;
                default: return false;
            }
        }
    }
}
