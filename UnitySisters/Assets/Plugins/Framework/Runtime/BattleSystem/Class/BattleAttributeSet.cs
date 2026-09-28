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
        public readonly AttributeSetInt hpAttribute = new();
        public readonly AttributeSetInt attackAttribute = new();
        public readonly AttributeSetInt defenseAttribute = new();
    }

}