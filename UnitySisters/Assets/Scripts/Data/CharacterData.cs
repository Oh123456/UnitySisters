using UnityEngine;
using UnitySisters.BattleSystem;
namespace UnitySisters.Table
{

    [CreateAssetMenu(fileName = "CharacterData", menuName = "Scriptable Objects/CharacterData")]
    public class CharacterData : ScriptableObject
    {
        [SerializeField] private CharacterBattleAttributeSet characterBattleAttributeSet;

        public CharacterBattleAttributeSet CharacterBattleAttributeSet => characterBattleAttributeSet;
    }

}