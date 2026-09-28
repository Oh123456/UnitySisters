using UnityEngine;
using UnityFramework;
using UnityFramework.BattleSystem;
using UnityFramework.Utility;

namespace UnitySisters.BattleSystem
{
    [System.Serializable]
    [DeepCopy]
    public partial class CharacterBattleAttributeSet : BattleAttributeSet
    {
        [DeepCopyWrapperField((nameof(AttributeSetInt.Value)))]
        [SerializeField] private AttributeSetInt hp = new();

        [DeepCopyWrapperField((nameof(AttributeSetFloat.Value)))]
        [SerializeField] private AttributeSetFloat attack = new();

        [DeepCopyWrapperField((nameof(AttributeSetFloat.Value)))]
        [SerializeField] private AttributeSetFloat defense = new();



        public AttributeSet<int> HP => hp;
        public AttributeSetFloat Attack => attack;
        public AttributeSetFloat Defense => defense;

    }

}