using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityFramework.FSM
{
    public class StatePattern<T> where T : System.Enum
    {
        private T currentState;

        public T CurrentState => this.currentState;

        /// <summary>
        /// (T,T) == (이전,바뀐것)
        /// </summary>
        public event System.Action<T, T> OnStateChanged;

        public StatePattern(T defulat)
        {
            currentState = defulat;
        }

        /// <summary>
        /// 상태 변환
        /// </summary>
        /// <param name="state"></param>
        public void ChangeState(T state)
        {
            T old = currentState;
            currentState = state;
            OnStateChanged?.Invoke(old, currentState);
        }

    }

}