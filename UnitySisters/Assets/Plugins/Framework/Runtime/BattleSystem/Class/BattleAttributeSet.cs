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
        public readonly AttributeSet<int> hpAttribute = new AttributeSet<int>();
        public readonly AttributeSet<int> attackAttribute = new AttributeSet<int>();
        public readonly AttributeSet<int> defenseAttribute = new AttributeSet<int>();
    }

}