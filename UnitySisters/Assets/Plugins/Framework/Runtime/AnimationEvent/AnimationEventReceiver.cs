using System;
using UnityEngine;

namespace UnityFramework.Animation
{
    public sealed class AnimationEventReceiver : MonoBehaviour
    {
        [SerializeField] private GameObject controlObject;

        public event Action<AnimationStateEventContext> OnAnimationStart;
        public event Action<AnimationStateEventContext> OnAnimationEnd;

        internal void NotifyAnimationStarted(AnimationStateEventContext animationStateEventContext)
        {
            OnAnimationStart?.Invoke(animationStateEventContext);
        }

        internal void NotifyAnimationEnded(AnimationStateEventContext animationStateEventContext)
        {
            OnAnimationEnd?.Invoke(animationStateEventContext);
        }

        public T GetInterface<T>() where T : class
        {
            if (!typeof(T).IsInterface)
                throw new InvalidOperationException();
            return controlObject.GetComponent<T>();
        }

    }

}