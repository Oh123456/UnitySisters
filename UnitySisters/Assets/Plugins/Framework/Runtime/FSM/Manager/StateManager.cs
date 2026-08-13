using System.Collections.Generic;
using UnityFramework.Singleton;

namespace UnityFramework.FSM
{

    public readonly struct StateKey : System.IEquatable<StateKey>
    {
        public readonly System.Type stateType;
        public readonly System.Type eumType;
        public readonly int stateId;

        public StateKey(System.Type stateType , System.Type eumType, int stateId)
        {
            this.stateType = stateType;
            this.eumType = eumType;
            this.stateId = stateId;
        }

        public bool Equals(StateKey other)
        {
            return (this.stateType.Equals(other.stateType) &&
                eumType.Equals(other.eumType) &&
                stateId.Equals(other.stateId));
        }

        public override int GetHashCode()
        {
            return System.HashCode.Combine(stateType, eumType, stateId);
        }
    }

    public class StateManager : LazySingleton<StateManager>
    {
        private Dictionary<StateKey, State> stateTable = new Dictionary<StateKey, State>();

        public bool AddState<T>(StateKey stateKey, T State) where T : State
        {
            return stateTable.TryAdd(stateKey, State);
        }

        public bool AddState<TState, TEnum>(int id, TState state) where TState : State where TEnum : System.Enum
        {
            return stateTable.TryAdd(new StateKey(typeof(TState), typeof(TEnum), id), state);
        }

        public bool TryGetState<T>(StateKey stateKey, out State state, System.Func<T> CreateState) where T : State
        {
            bool isValid = stateTable.TryGetValue(stateKey, out state);
            if (!isValid)
            {
                state = CreateState();
                isValid = AddState(stateKey, state);
            }
            return isValid;
        }

        public bool TryGetState<TState, TEnum>(int id, out State state) where TState : State where TEnum : System.Enum
        {
            return stateTable.TryGetValue(new StateKey(typeof(TState), typeof(TEnum), id), out state);
        }
    }
}
