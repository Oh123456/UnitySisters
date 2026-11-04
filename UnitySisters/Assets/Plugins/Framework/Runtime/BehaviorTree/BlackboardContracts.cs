using System;
using UnityEngine;

namespace UnityFramework.BehaviorTree
{
    public enum BlackboardKeyType { Bool, Int, Float, Vector3, Object }

    [Serializable]
    public abstract class BlackboardKeyReference
    {
        [SerializeField] private string id;
        public string ID { get => id; }
        internal abstract Type ValueType { get; }
        private protected BlackboardKeyReference(string id) { this.id = id; }
    }

    [Serializable]
    public sealed class BlackboardKeyReference<T> : BlackboardKeyReference
    {
        internal override Type ValueType { get => typeof(T); }
        public BlackboardKeyReference() : base(null) { }
        internal BlackboardKeyReference(string id) : base(id) { }
    }

    public interface IReadOnlyBlackboard
    {
        T GetValue<T>(BlackboardKeyReference<T> key);
        bool TryGetValue<T>(BlackboardKeyReference<T> key, out T value);
        event Action<string> KeyChanged;
    }

    public interface IBlackboardWriter
    {
        void SetValue<T>(BlackboardKeyReference<T> key, T value);
        void ResetValue(BlackboardKeyReference key);
    }
}
