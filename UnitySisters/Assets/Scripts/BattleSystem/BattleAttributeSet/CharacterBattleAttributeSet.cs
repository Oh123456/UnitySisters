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
        [DeepCopyWrapperField((nameof(ReflectionProperty<int>.Value)))]
        [SerializeField] private ReflectionProperty<int> hp = new();

        [DeepCopyWrapperField((nameof(ReflectionProperty<float>.Value)))]
        [SerializeField] private ReflectionProperty<float> attack = new();

        [DeepCopyWrapperField((nameof(ReflectionProperty<float>.Value)))]
        [SerializeField] private ReflectionProperty<float> defense = new();



        public ReflectionProperty<int> HP => hp;
        public ReflectionProperty<float> Attack => attack;
        public ReflectionProperty<float> Defense => defense;

    }

}