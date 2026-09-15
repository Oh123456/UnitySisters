using UnityEngine;
using UnityFramework.Animation;
using UnitySisters.FSM;

namespace UnitySisters
{
    [System.Serializable]
    public class AttackEndAnimationEventCommand : AnimationEventCommand
    {
        public override void Execute(AnimationEventReceiver animationEventReceiver)
        {
            IFSMController fsmController = animationEventReceiver.GetInterface<IFSMController>();
            if (fsmController == null)
                return;

            fsmController.SetDefaultState();
        }
    }
}