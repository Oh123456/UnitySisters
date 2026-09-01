using UnityEngine;
using UnityFramework.Animation;
using UnitySisters.Controller;

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
            IAttackController attackController = animationEventReceiver.GetInterface<IAttackController>();
            if (attackController == null)
                return;

            attackController.Attack(new AttackAnimationEventContext()
            {
                attackKey = attackKey,
            });
        }
    }
}
