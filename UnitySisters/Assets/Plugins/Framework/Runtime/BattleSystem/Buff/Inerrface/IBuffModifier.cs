using UnityEngine;

namespace UnityFramework.BattleSystem
{
    public interface IBuffModifier
    {
        public int Priority { get; }
    }

    public interface IBuffModifier<T> : IBuffModifier
    {
        public T Modifiy(T inValue);
    }

}