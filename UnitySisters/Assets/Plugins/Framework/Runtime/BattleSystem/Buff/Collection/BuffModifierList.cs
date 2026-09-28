using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityFramework.BattleSystem
{
    public class BuffModifierList<T> : IBuffModifierCollection<T>
    {
        public struct Enumerator : IEnumerator<BuffModifierBinding<T>>, IEnumerator, IDisposable
        {
            private List<BuffModifierBinding<T>>.Enumerator enumerator;
            public BuffModifierBinding<T> Current
            {
                get
                {
                    return enumerator.Current;
                }
            }

            object IEnumerator.Current => Current;

            public Enumerator(List<BuffModifierBinding<T>> buffModifiers)
            {
                enumerator = buffModifiers.GetEnumerator();
            }

            public void Dispose()
            {
                enumerator.Dispose();
            }

            public bool MoveNext()
            {
                return enumerator.MoveNext();
            }

            bool IEnumerator.MoveNext()
            {
                return enumerator.MoveNext();
            }

            void IEnumerator.Reset()
            {
                if (this.enumerator is IEnumerator enumerator)
                    enumerator.Reset();
            }
        }

        private List<BuffModifierBinding<T>> buffModifiers = new();

        private bool isDirty = false;

        public int Count => buffModifiers.Count;

        public void Add(BuffModifierBinding<T> buffModifierBinding)
        {
            buffModifiers.Add(buffModifierBinding);
            isDirty = true;
        }

        public bool Remove(BuffModifierBinding<T> buffModifierBinding)
        {
            if (!buffModifiers.Remove(buffModifierBinding))
                return false;

            isDirty = true;
            return true;
        }

        private void EnsureSorted()
        {
            if (!isDirty)
                return;

            buffModifiers.Sort(static (a, b) => a.buffModifier.Priority.CompareTo(b.buffModifier.Priority));
            isDirty = false;
        }

        public void Clear()
        {
            buffModifiers.Clear();
        }

        public IEnumerable<BuffModifierBinding<T>> Enumerate()
        {
            EnsureSorted();
            return buffModifiers;
        }

        public Enumerator GetEnumerator()
        {
            EnsureSorted();
            return new Enumerator(buffModifiers);
        }






    }

    public readonly struct BuffModifierBinding<T> : System.IEquatable<BuffModifierBinding<T>>
    {
        public readonly IBuffModifier<T> buffModifier;
        public readonly IBuffInstance buffInstance;

        public BuffModifierBinding(IBuffModifier<T> buffModifier, IBuffInstance buffInstance)
        {
            this.buffModifier = buffModifier;
            this.buffInstance = buffInstance;
        }

        public bool Equals(BuffModifierBinding<T> other)
        {
            return ReferenceEquals(buffModifier, other.buffModifier) &&
                   ReferenceEquals(buffInstance, other.buffInstance);
        }
    }

}