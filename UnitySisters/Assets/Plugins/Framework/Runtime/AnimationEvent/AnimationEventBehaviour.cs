using System.Collections.Generic;
using UnityEngine;

namespace UnityFramework.Animation
{
    public class AnimationEventBehaviour : StateMachineBehaviour
    {
        [SerializeField] private AnimationEventData eventData;

        private AnimationEventReceiver animationEventReceiver;
        private IReadOnlyList<AnimationEventCommand> animationEventCommands;

        private uint triggeredMask = 0;
        private int loopCount = 0;

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            animationEventReceiver = animator.gameObject.GetComponent<AnimationEventReceiver>();
            animationEventCommands = eventData.Events;
        }

        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            float normalizedTime = stateInfo.normalizedTime;
            int currentLoopCount = Mathf.FloorToInt(normalizedTime);

            if (currentLoopCount != loopCount)
            {
                ResetTrigger();
                loopCount = currentLoopCount;
            }

            float time = normalizedTime - currentLoopCount;

            int count = animationEventCommands.Count;
            for (int i = 0; i < count; i++)
            {
                var command = animationEventCommands[i];
                if (command == null)
                    continue;

                // 시간순서대로 정렬되있음
                if (time < command.StartTime)
                    break;

                bool isTriggered = IsTriggered(i);

                if (command.EventType == AnimationEventCommandType.Trigger)
                {
                    if (isTriggered)
                        continue;

                    command.Execute(animationEventReceiver);
                    SetTriggered(i);
                }
                else
                {
                    if (time >= command.EndTime)
                    {
                        if (isTriggered)
                        {
                            command.ContinuousEventExit(animationEventReceiver);
                            RemoveTriggered(i);
                        }
                        continue;
                    }

                    if (!isTriggered)
                    {
                        command.ContinuousEventEnter(animationEventReceiver);
                        SetTriggered(i);
                    }


                    command.Execute(animationEventReceiver);

                }

            }

        }

        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            int count = animationEventCommands.Count;
            for (int i = 0; i < count; i++)
            {
                var command = animationEventCommands[i];

                if (command == null ||
                    !IsTriggered(i) ||
                    command.EventType != AnimationEventCommandType.Continuous)
                    continue;

                command.ContinuousEventExit(animationEventReceiver);

            }
            animationEventReceiver = null;
            animationEventCommands = null;
            ResetTrigger();
            loopCount = 0;
        }

        private bool IsTriggered(int index)
        {
            return (triggeredMask & (1u << index)) != 0;
        }

        private void SetTriggered(int index)
        {
            triggeredMask |= (1u << index);
        }

        private void RemoveTriggered(int index)
        {
            triggeredMask &= ~(1u << index);
        }

        private void ResetTrigger()
        {
            triggeredMask = 0;
        }
    } 
}
