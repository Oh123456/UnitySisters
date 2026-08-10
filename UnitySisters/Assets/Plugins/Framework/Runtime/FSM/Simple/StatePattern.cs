using System.Collections.Generic;

namespace UnityFramework.FSM
{
    public class StatePattern<T> where T : System.Enum
    {
        private T currentState;

        public T CurrentState => currentState;

        /// <summary>
        /// (T,T) == (이전,바뀐것)
        /// </summary>
        public event System.Action<T, T> OnStateChanged;

        public StatePattern(T defaultState)
        {
            currentState = defaultState;
        }

        /// <summary>
        /// 상태 변환
        /// </summary>
        /// <param name="state"></param>
        public void ChangeState(T state)
        {
            if (EqualityComparer<T>.Default.Equals(currentState, state))
                return;

            T old = currentState;
            currentState = state;
            OnStateChanged?.Invoke(old, currentState);
        }

    }

}
