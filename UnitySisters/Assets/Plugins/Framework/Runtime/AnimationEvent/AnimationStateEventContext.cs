using UnityEngine;

namespace UnityFramework.Animation
{
    public readonly struct AnimationStateEventContext
    {
        public readonly Animator animator;
        public readonly AnimationEventData eventData;

        public AnimationStateEventContext(
            Animator animator,
            AnimationEventData eventData)
        {
            this.animator = animator;
            this.eventData = eventData;
        }
    }
}