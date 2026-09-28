using System.Collections.Generic;
using UnityEngine;

namespace UnityFramework.BattleSystem
{
    public interface IBuffModifierCollection<T>
    {
        public int Count { get; }
        void Add(BuffModifierBinding<T> buffModifierBinding);
        bool Remove(BuffModifierBinding<T> buffModifierBinding);
        public void Clear();
        IEnumerable<BuffModifierBinding<T>> Enumerate();
    }

}