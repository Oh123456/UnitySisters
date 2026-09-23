using System.Collections.Generic;
using UnityEngine;

namespace UnityFramework.BattleSystem
{
    public interface IBuffModifierCollection<T>
    {
        public int Count { get; }
        void Add(IBuffModifier<T> modifier);
        bool Remove(IBuffModifier<T> modifier);
        public void Clear();
        IEnumerable<IBuffModifier<T>> Enumerate();
    }

}