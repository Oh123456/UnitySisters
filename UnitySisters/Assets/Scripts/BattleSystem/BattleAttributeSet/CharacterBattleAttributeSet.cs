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
        [DeepCopyWrapperField((nameof(AttributeSet<int>.Value)))]
        [SerializeField] private AttributeSet<int> hp = new();

        [DeepCopyWrapperField((nameof(AttributeSet<float>.Value)))]
        [SerializeField] private AttributeSet<float> attack = new();

        [DeepCopyWrapperField((nameof(AttributeSet<float>.Value)))]
        [SerializeField] private AttributeSet<float> defense = new();



        public AttributeSet<int> HP => hp;
        public AttributeSet<float> Attack => attack;
        public AttributeSet<float> Defense => defense;

    }

}