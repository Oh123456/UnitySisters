using UnityEngine;
using UnityFramework.Animation;
using UnityFramework.BattleSystem;

namespace UnitySisters
{   
    public struct AttackAnimationEventContext
    {
        public string attackKey;
    }

    [System.Serializable]
    public class AttackAnimationEventCommand : AnimationEventCommand
    {
        [SerializeField,AttackDataKey] private string attackKey;

        public override void Execute(AnimationEventReceiver animationEventReceiver)
        {
            IAttackAble attackAble = animationEventReceiver.GetInterface<IAttackAble>();
            if (attackAble == null)
                return;

            attackAble.Attack(new AttackAnimationEventContext()
            {
                attackKey = attackKey,
            });
        }
    }
}
