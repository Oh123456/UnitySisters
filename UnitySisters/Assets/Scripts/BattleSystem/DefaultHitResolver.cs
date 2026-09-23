using UnityEngine;
using UnityFramework.BattleSystem;

namespace UnitySisters.BattleSystem
{
    [System.Serializable]
    public class DefaultHitResolver : HitResolver<DefaultHitResult>
    {        
        public override DefaultHitResult Hit(in HitInfo hitInfo, BattleComponent soureBattleComponent)
        {
            CharacterBattleAttributeSet soureAttrubyteSet = soureBattleComponent.GetBattleAttributeSet<CharacterBattleAttributeSet>();
            CharacterBattleAttributeSet hitIAttrubyteSet = hitInfo.hitBattleComponent.GetBattleAttributeSet<CharacterBattleAttributeSet>();

            float soureDefense = soureAttrubyteSet.Defense;
            float hitAttack = hitIAttrubyteSet.Attack;
            int hp = soureAttrubyteSet.HP;
            hp = (int)((float)hp - (hitAttack - soureDefense));

            soureAttrubyteSet.HP.FinalValue = hp;

            if (hp <= 0)
                Debug.Log("다이");

            DefaultHitResult hitResult = new DefaultHitResult()
            {
                defaultHitResultType = DefaultHitResultType.Hit,
            };

            return hitResult;
        }
    }
}
