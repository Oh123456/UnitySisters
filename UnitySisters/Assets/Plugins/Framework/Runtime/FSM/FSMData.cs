using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityFramework.FSM
{
    [Serializable]
    public sealed class FSMStateData
    {
        [SerializeField] private int id;
        [SerializeField] private string name;
        [SerializeField] private Vector2 position;

        public int ID => this.id;
        public string Name => this.name;
        public Vector2 Position => this.position;

        internal FSMStateData(int id, string name, Vector2 position)
        {
            this.id = id;
            this.name = name;
            this.position = position;
        }

        /// <summary>
        /// 상태 이름 변경
        /// </summary>
        public void SetName(string name)
        {
            this.name = string.IsNullOrWhiteSpace(name) ? $"State {this.id}" : name.Trim();
        }

        /// <summary>
        /// 에디터 그래프에서 사용할 노드 위치 저장
        /// </summary>
        public void SetPosition(Vector2 position)
        {
            this.position = position;
        }
    }

    [Serializable]
    public sealed class FSMTransitionData
    {
        [SerializeField] private int fromStateID;
        [SerializeField] private int toStateID;
        [SerializeField] private string name;
        [SerializeField] private string conditionKey;
        [SerializeField] private int priority;

        public int FromStateID => this.fromStateID;
        public int ToStateID => this.toStateID;
        public string Name => this.name;
        public string ConditionKey => this.conditionKey;
        public int Priority => this.priority;

        internal FSMTransitionData(int fromStateID, int toStateID)
        {
            this.fromStateID = fromStateID;
            this.toStateID = toStateID;
            this.name = $"{fromStateID} To {toStateID}";
            this.conditionKey = string.Empty;
        }

        /// <summary>
        /// 뷰어와 로그에 표시할 전이 이름 변경
        /// </summary>
        public void SetName(string name)
        {
            this.name = string.IsNullOrWhiteSpace(name)
                ? $"{this.fromStateID} To {this.toStateID}"
                : name.Trim();
        }

        /// <summary>
        /// 게임 코드에서 조건 함수를 찾을 때 사용할 키 변경
        /// </summary>
        public void SetConditionKey(string conditionKey)
        {
            this.conditionKey = conditionKey?.Trim() ?? string.Empty;
        }

        /// <summary>
        /// 동일한 출발지와 목적지 사이에서 먼저 평가할 전이 우선순위 변경
        /// </summary>
        public void SetPriority(int priority)
        {
            this.priority = priority;
        }
    }

    [CreateAssetMenu(fileName = "FSMData", menuName = "FSM/State Machine")]
    public class FSMData : ScriptableObject
    {
        [SerializeField] private int initialStateID;
        [SerializeField] private int nextStateID;
        [SerializeField] private List<FSMStateData> states = new List<FSMStateData>();
        [SerializeField] private List<FSMTransitionData> transitions = new List<FSMTransitionData>();

        public int InitialStateID => this.initialStateID;
        public IReadOnlyList<FSMStateData> States => this.states;
        public IReadOnlyList<FSMTransitionData> Transitions => this.transitions;

        /// <summary>
        /// 새 상태를 추가하고 에셋 내부에서 중복되지 않는 ID를 발급
        /// </summary>
        public FSMStateData AddState(string name, Vector2 position)
        {
            int stateID = GetNextStateID();
            var state = new FSMStateData(stateID, name, position);
            state.SetName(name);
            this.states.Add(state);

            if (this.states.Count == 1)
                this.initialStateID = stateID;

            return state;
        }

        /// <summary>
        /// 상태와 해당 상태에 연결된 모든 전이 제거
        /// </summary>
        public bool RemoveState(int stateID)
        {
            int removedCount = this.states.RemoveAll(state => state.ID == stateID);
            if (removedCount == 0)
                return false;

            this.transitions.RemoveAll(transition =>
                transition.FromStateID == stateID || transition.ToStateID == stateID);

            if (this.states.Count > 0 && !ContainsState(this.initialStateID))
                this.initialStateID = this.states[0].ID;

            return true;
        }

        /// <summary>
        /// 두 상태를 연결하는 전이 추가
        /// </summary>
        public FSMTransitionData AddTransition(int fromStateID, int toStateID)
        {
            if (!ContainsState(fromStateID))
                throw new ArgumentException($"Source state ID {fromStateID} does not exist.", nameof(fromStateID));
            if (!ContainsState(toStateID))
                throw new ArgumentException($"Target state ID {toStateID} does not exist.", nameof(toStateID));

            var transition = new FSMTransitionData(fromStateID, toStateID);
            this.transitions.Add(transition);
            return transition;
        }

        /// <summary>
        /// 지정한 전이 제거
        /// </summary>
        public bool RemoveTransition(FSMTransitionData transition)
        {
            return transition != null && this.transitions.Remove(transition);
        }

        /// <summary>
        /// 상태 머신이 시작할 상태 지정
        /// </summary>
        public void SetInitialStateID(int stateID)
        {
            if (!ContainsState(stateID))
                throw new ArgumentException($"Initial state ID {stateID} does not exist.", nameof(stateID));

            this.initialStateID = stateID;
        }

        /// <summary>
        /// 상태 ID에 해당하는 직렬화 데이터 검색
        /// </summary>
        public FSMStateData FindState(int stateID)
        {
            return this.states.Find(state => state.ID == stateID);
        }

        /// <summary>
        /// 저장된 구조와 게임 코드의 상태·조건 팩토리를 결합해 실행 가능한 상태 머신 생성
        /// </summary>
        /// <param name="owner">상태에서 참조할 게임 객체</param>
        /// <param name="stateFactory">상태 데이터를 실제 State 객체로 변환하는 함수</param>
        /// <param name="conditionFactory">조건 키가 있는 전이를 실제 조건 함수로 변환하는 함수</param>
        public StateMachine CreateStateMachine(
            object owner,
            Func<FSMStateData, State> stateFactory,
            Func<FSMTransitionData, Func<IStateMachine, bool>> conditionFactory = null)
        {
            if (stateFactory == null)
                throw new ArgumentNullException(nameof(stateFactory));

            ValidateDefinition();
            var stateMachine = new StateMachine(owner, this);

            // FSMData는 구조만 보관한다. 실제 행동 객체는 게임 코드의 팩토리가 생성해야 한다.
            for (int i = 0; i < this.states.Count; i++)
            {
                FSMStateData stateData = this.states[i];
                State state = stateFactory.Invoke(stateData);
                if (state == null)
                    throw new InvalidOperationException($"State factory returned null for state ID {stateData.ID}.");
                if (state.ID != stateData.ID)
                    throw new InvalidOperationException(
                        $"State factory returned ID {state.ID}, but FSMData expected ID {stateData.ID}.");

                stateMachine.AddState(state);
            }

            for (int i = 0; i < this.transitions.Count; i++)
            {
                FSMTransitionData transitionData = this.transitions[i];
                Func<IStateMachine, bool> condition = null;

                if (!string.IsNullOrWhiteSpace(transitionData.ConditionKey))
                {
                    if (conditionFactory == null)
                        throw new InvalidOperationException(
                            $"Transition '{transitionData.Name}' requires condition key " +
                            $"'{transitionData.ConditionKey}', but no condition factory was provided.");

                    condition = conditionFactory.Invoke(transitionData);
                    if (condition == null)
                        throw new InvalidOperationException(
                            $"Condition factory could not resolve key '{transitionData.ConditionKey}'.");
                }

                stateMachine.AddTransition(new StateTransition(
                    transitionData.FromStateID,
                    transitionData.ToStateID,
                    condition,
                    transitionData.Priority,
                    transitionData.Name));
            }

            return stateMachine;
        }

        /// <summary>
        /// 실행 전에 중복 ID, 시작 상태와 끊어진 전이를 검사
        /// </summary>
        public void ValidateDefinition()
        {
            if (this.states.Count == 0)
                throw new InvalidOperationException($"FSMData '{this.name}' has no states.");

            var stateIDs = new HashSet<int>();
            for (int i = 0; i < this.states.Count; i++)
            {
                FSMStateData state = this.states[i];
                if (state == null)
                    throw new InvalidOperationException($"FSMData '{this.name}' contains a null state.");
                if (!stateIDs.Add(state.ID))
                    throw new InvalidOperationException($"FSMData '{this.name}' contains duplicate state ID {state.ID}.");
            }

            if (!stateIDs.Contains(this.initialStateID))
                throw new InvalidOperationException(
                    $"FSMData '{this.name}' has unknown initial state ID {this.initialStateID}.");

            for (int i = 0; i < this.transitions.Count; i++)
            {
                FSMTransitionData transition = this.transitions[i];
                if (transition == null)
                    throw new InvalidOperationException($"FSMData '{this.name}' contains a null transition.");
                if (!stateIDs.Contains(transition.FromStateID) || !stateIDs.Contains(transition.ToStateID))
                    throw new InvalidOperationException(
                        $"Transition '{transition.Name}' in FSMData '{this.name}' references an unknown state.");
            }
        }

        /// <summary>
        /// 삭제된 상태 ID를 재사용하지 않는 다음 ID 계산
        /// </summary>
        private int GetNextStateID()
        {
            while (ContainsState(this.nextStateID))
                this.nextStateID++;

            return this.nextStateID++;
        }

        private bool ContainsState(int stateID)
        {
            return this.states.Exists(state => state.ID == stateID);
        }
    }
}
