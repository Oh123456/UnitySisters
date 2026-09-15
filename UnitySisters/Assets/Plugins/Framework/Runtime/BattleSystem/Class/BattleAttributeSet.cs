using UnityEngine;

namespace UnityFramework.BattleSystem
{
    [System.Serializable]
    public abstract class BattleAttributeSet
    {
        
    }

    [System.Serializable]
    public class DefaultAttributeSet : BattleAttributeSet
    {
        public readonly ReflectionProperty<int> hpAttribute = new ReflectionProperty<int>();
        public readonly ReflectionProperty<int> attackAttribute = new ReflectionProperty<int>();
        public readonly ReflectionProperty<int> defenseAttribute = new ReflectionProperty<int>();
    }

}