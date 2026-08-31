using System;

using UnityEngine;
namespace UnityFramework.Animation
{
    public enum AnimationEventCommandType
    {
        Trigger,
        Continuous
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class AnimationEventCommandTypeAttribute : Attribute
    {
        public AnimationEventCommandType EventType { get; }

        public AnimationEventCommandTypeAttribute(AnimationEventCommandType eventType)
        {
            EventType = eventType;
        }
    }

    [System.Serializable]
    public abstract partial class AnimationEventCommand
    {
      

        [SerializeField] private AnimationEventCommandType evetType;
        [SerializeField, Range(0.0f, 1.0f)] private float startTime;
        [SerializeField, Range(0.0f, 1.0f)] private float endTime;

        public float StartTime => startTime;
        public float EndTime => endTime;
        public AnimationEventCommandType EventType => evetType;

        public abstract void Execute(AnimationEventReceiver animationEventReceiver);

        public virtual void ContinuousEventEnter(AnimationEventReceiver animationEventReceiver) { }
        public virtual void ContinuousEventExit(AnimationEventReceiver animationEventReceiver) { }
    }

}
