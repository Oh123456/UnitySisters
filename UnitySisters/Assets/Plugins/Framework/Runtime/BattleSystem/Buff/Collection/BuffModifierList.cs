using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityFramework.BattleSystem
{
    public class BuffModifierList<T> : IBuffModifierCollection<T>
    {
        public struct Enumerator : IEnumerator<IBuffModifier<T>>, IEnumerator, IDisposable
        {
            private List<IBuffModifier<T>>.Enumerator enumerator;
            public IBuffModifier<T> Current
            {
                get
                {
                    return enumerator.Current;
                }
            }

            object IEnumerator.Current => Current;

            public Enumerator(List<IBuffModifier<T>> buffModifiers)
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

        private List<IBuffModifier<T>> buffModifiers = new();

        private bool isDirty = false;

        public int Count => buffModifiers.Count;

        public void Add(IBuffModifier<T> modifier)
        {
            buffModifiers.Add(modifier);
            isDirty = true;
        }

        public bool Remove(IBuffModifier<T> modifier)
        {
            if (!buffModifiers.Remove(modifier))
                return false;

            isDirty = true;
            return true;
        }

        private void EnsureSorted()
        {
            if (!isDirty)
                return;

            buffModifiers.Sort(static (a, b) => a.Priority.CompareTo(b.Priority));
        }

        public void Clear()
        {
            buffModifiers.Clear();
        }

        public IEnumerable<IBuffModifier<T>> Enumerate()
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

}