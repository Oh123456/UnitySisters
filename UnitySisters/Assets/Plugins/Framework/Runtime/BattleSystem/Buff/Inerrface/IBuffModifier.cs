using UnityEngine;

namespace UnityFramework.BattleSystem
{
    public interface IBuffModifier
    {
        public int Priority { get; }
    }

    public interface IBuffModifier<T> : IBuffModifier
    {
        public void Accumulate(T baseValue, BuffModifierContext buffModifierContext, IBuffInstance buffInstance);
    }

}