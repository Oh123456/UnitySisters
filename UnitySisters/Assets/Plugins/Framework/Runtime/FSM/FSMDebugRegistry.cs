using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace UnityFramework.FSM
{
    public static class FSMDebugRegistry
    {
        private static readonly List<WeakReference<IStateMachine>> stateMachines =
            new List<WeakReference<IStateMachine>>();

        /// <summary>
        /// 실행 중인 상태 머신을 디버그 목록에 등록
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void Register(IStateMachine stateMachine)
        {
            if (stateMachine == null)
                throw new ArgumentNullException(nameof(stateMachine));

            RemoveInvalidStateMachines();
            for (int i = 0; i < stateMachines.Count; i++)
            {
                if (stateMachines[i].TryGetTarget(out IStateMachine registeredStateMachine) &&
                    ReferenceEquals(registeredStateMachine, stateMachine))
                    return;
            }

            stateMachines.Add(new WeakReference<IStateMachine>(stateMachine));
        }

        /// <summary>
        /// 지정한 상태 머신을 디버그 목록에서 제거
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void Unregister(IStateMachine stateMachine)
        {
            for (int i = stateMachines.Count - 1; i >= 0; i--)
            {
                if (!stateMachines[i].TryGetTarget(out IStateMachine registeredStateMachine) ||
                    ReferenceEquals(registeredStateMachine, stateMachine))
                    stateMachines.RemoveAt(i);
            }
        }

        /// <summary>
        /// 현재 유효한 상태 머신 목록을 전달받은 리스트에 복사
        /// </summary>
        public static void GetStateMachines(List<IStateMachine> results)
        {
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            results.Clear();
            for (int i = stateMachines.Count - 1; i >= 0; i--)
            {
                if (!stateMachines[i].TryGetTarget(out IStateMachine stateMachine))
                {
                    stateMachines.RemoveAt(i);
                    continue;
                }

                results.Add(stateMachine);
            }
        }

        /// <summary>
        /// 도메인 리로드를 사용하지 않는 Play Mode에서도 이전 실행 정보 제거
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Clear()
        {
            stateMachines.Clear();
        }

        /// <summary>
        /// 가비지 컬렉션된 상태 머신 참조 제거
        /// </summary>
        private static void RemoveInvalidStateMachines()
        {
            for (int i = stateMachines.Count - 1; i >= 0; i--)
            {
                if (!stateMachines[i].TryGetTarget(out _))
                    stateMachines.RemoveAt(i);
            }
        }
    }
}
