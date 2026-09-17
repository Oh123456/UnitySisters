using System;
using UnityEngine;

namespace UnityFramework
{
    public interface IReadOnlyReflectionProperty<T> where T : IEquatable<T>
    {
        public T Value { get; }
        public event Action<T> OnChanged;
    }
}