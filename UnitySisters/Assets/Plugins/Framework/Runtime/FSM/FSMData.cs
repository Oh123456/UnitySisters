using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace UnityFramework.FSM
{
    [Serializable]
    public sealed class FSMStateData
    {
        [SerializeField] private int id;
        [SerializeField] private string name;
#if UNITY_EDITOR
        [SerializeField] private Vector2 position;
#endif

        public int ID => this.id;
        public string Name => this.name;
#if UNITY_EDITOR
        public Vector2 Position => this.position;

        internal FSMStateData(int id, string name, Vector2 position)
        {
            this.id = id;
            this.name = name;
            this.position = position;
        }
#endif

        /// <summary>
        /// 상태 이름 변경
        /// </summary>
        public void SetName(string name)
        {
            this.name = string.IsNullOrWhiteSpace(name) ? $"State {this.id}" : name.Trim();
        }

#if UNITY_EDITOR
        /// <summary>
        /// 에디터 그래프에서 사용할 노드 위치 저장
        /// </summary>
        public void SetPosition(Vector2 position)
        {
            this.position = position;
        }
#endif
    }

    [Serializable]
    public sealed class FSMTransitionData
    {
        [SerializeField] private int fromStateID;
        [SerializeField] private int toStateID;
        [SerializeField] private string name;
        [SerializeField] private bool hasCondition;
        [FormerlySerializedAs("conditionValue")]
        [SerializeField] private int conditionID;
        [SerializeField] private int priority;
#if UNITY_EDITOR
        [SerializeField] private List<Vector2> routePoints = new List<Vector2>();
#endif

        public int FromStateID => this.fromStateID;
        public int ToStateID => this.toStateID;
        public string Name => this.name;
        public bool HasCondition => this.hasCondition;
        public int ConditionID => this.conditionID;
        public int Priority => this.priority;
#if UNITY_EDITOR
        public IReadOnlyList<Vector2> RoutePoints =>
            this.routePoints ?? (IReadOnlyList<Vector2>)Array.Empty<Vector2>();
#endif

        internal FSMTransitionData(int fromStateID, int toStateID)
        {
            this.fromStateID = fromStateID;
            this.toStateID = toStateID;
            this.name = $"{fromStateID} To {toStateID}";
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
        /// 선택된 조건 enum의 숫자 값을 전이에 지정
        /// </summary>
        public void SetCondition(int conditionID)
        {
            this.hasCondition = true;
            this.conditionID = conditionID;
        }

        /// <summary>
        /// 전이에 지정된 조건 제거
        /// </summary>
        public void ClearCondition()
        {
            this.hasCondition = false;
            this.conditionID = 0;
        }

        /// <summary>
        /// 동일한 출발지와 목적지 사이에서 먼저 평가할 전이 우선순위 변경
        /// </summary>
        public void SetPriority(int priority)
        {
            this.priority = priority;
        }

#if UNITY_EDITOR
        /// <summary>
        /// 전이선의 지정 구간에 경로 고정 핀 추가
        /// </summary>
        public void AddRoutePoint(int index, Vector2 position)
        {
            EnsureRoutePoints();
            if (index < 0 || index > this.routePoints.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            this.routePoints.Insert(index, position);
        }

        /// <summary>
        /// 경로 고정 핀 위치 변경
        /// </summary>
        public void SetRoutePoint(int index, Vector2 position)
        {
            EnsureRoutePoints();
            if (index < 0 || index >= this.routePoints.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            this.routePoints[index] = position;
        }

        /// <summary>
        /// 지정한 경로 고정 핀 제거
        /// </summary>
        public void RemoveRoutePoint(int index)
        {
            EnsureRoutePoints();
            if (index < 0 || index >= this.routePoints.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            this.routePoints.RemoveAt(index);
        }

        /// <summary>
        /// 전이선의 모든 경로 고정 핀 제거
        /// </summary>
        public void ClearRoutePoints()
        {
            this.routePoints?.Clear();
        }

        private void EnsureRoutePoints()
        {
            if (this.routePoints == null)
                this.routePoints = new List<Vector2>();
        }
#endif
    }

    [CreateAssetMenu(fileName = "FSMData", menuName = "FSM/State Machine")]
    public class FSMData : ScriptableObject
    {
        [SerializeField] private int initialStateID;
#if UNITY_EDITOR
        [SerializeField] private int nextStateID;
        [SerializeField] private string stateIDTypeID;
        [SerializeField] private string conditionTypeID;
#endif
        [SerializeField] private List<FSMStateData> states = new List<FSMStateData>();
        [SerializeField] private List<FSMTransitionData> transitions = new List<FSMTransitionData>();

        public int InitialStateID => this.initialStateID;
#if UNITY_EDITOR
        public string StateIDTypeID => this.stateIDTypeID;
        public string ConditionTypeID => this.conditionTypeID;
#endif
        public IReadOnlyList<FSMStateData> States => this.states;
        public IReadOnlyList<FSMTransitionData> Transitions => this.transitions;

#if UNITY_EDITOR
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
        /// State ID enum에서 선택한 숫자로 새 상태 추가
        /// </summary>
        public FSMStateData AddState(int stateID, string name, Vector2 position)
        {
            if (ContainsState(stateID))
                throw new ArgumentException($"State ID {stateID} already exists.", nameof(stateID));

            var state = new FSMStateData(stateID, name, position);
            state.SetName(name);
            this.states.Add(state);

            if (this.states.Count == 1)
                this.initialStateID = stateID;
            return state;
        }
#endif

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
        /// 조건이 없는 저장 구조와 상태 팩토리를 결합해 실행 가능한 상태 머신 생성
        /// </summary>
        /// <param name="owner">상태에서 참조할 게임 객체</param>
        /// <param name="stateFactory">상태 데이터를 실제 State 객체로 변환하는 함수</param>
        public StateMachine CreateStateMachine(
            object owner,
            Func<FSMStateData, State> stateFactory)
        {
            return CreateStateMachineInternal(owner, stateFactory, null);
        }

        /// <summary>
        /// 저장된 조건 ID와 게임 코드의 조건 팩토리를 결합해 실행 가능한 상태 머신 생성
        /// </summary>
        public StateMachine CreateStateMachine(
            object owner,
            Func<FSMStateData, State> stateFactory,
            Func<int, Func<IStateMachine, bool>> conditionFactory)
        {
            if (conditionFactory == null)
                throw new ArgumentNullException(nameof(conditionFactory));

            return CreateStateMachineInternal(owner, stateFactory, conditionFactory);
        }

        private StateMachine CreateStateMachineInternal(
            object owner,
            Func<FSMStateData, State> stateFactory,
            Func<int, Func<IStateMachine, bool>> conditionFactory)
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

                if (transitionData.HasCondition)
                {
                    if (conditionFactory == null)
                        throw new InvalidOperationException(
                            $"Transition '{transitionData.Name}' requires a condition, " +
                            "but no condition factory was provided.");

                    condition = conditionFactory.Invoke(transitionData.ConditionID);
                    if (condition == null)
                        throw new InvalidOperationException(
                            $"Condition factory returned null for transition '{transitionData.Name}'.");
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

            for (int i = 0; i < this.states.Count; i++)
            {
                FSMStateData state = this.states[i];
                if (state == null)
                    throw new InvalidOperationException($"FSMData '{this.name}' contains a null state.");

                // FSM 생성마다 검증용 HashSet을 할당하지 않도록 앞선 항목만 직접 비교한다.
                for (int duplicateIndex = 0; duplicateIndex < i; duplicateIndex++)
                {
                    if (this.states[duplicateIndex].ID == state.ID)
                        throw new InvalidOperationException(
                            $"FSMData '{this.name}' contains duplicate state ID {state.ID}.");
                }
            }

            if (!ContainsState(this.initialStateID))
                throw new InvalidOperationException(
                    $"FSMData '{this.name}' has unknown initial state ID {this.initialStateID}.");

            for (int i = 0; i < this.transitions.Count; i++)
            {
                FSMTransitionData transition = this.transitions[i];
                if (transition == null)
                    throw new InvalidOperationException($"FSMData '{this.name}' contains a null transition.");
                if (!ContainsState(transition.FromStateID) || !ContainsState(transition.ToStateID))
                    throw new InvalidOperationException(
                        $"Transition '{transition.Name}' in FSMData '{this.name}' references an unknown state.");
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// FSMData의 조건 ID를 에디터에서 표시할 enum 타입 지정
        /// </summary>
        /// <remarks>
        /// 타입이 바뀌면 같은 숫자가 다른 조건으로 해석될 수 있으므로 기존 조건 값을 모두 제거한다.
        /// </remarks>
        public void SetConditionType(Type conditionType)
        {
            if (conditionType != null && !FSMConditionType.IsValid(conditionType))
                throw new ArgumentException(
                    "Condition type must be a unique, non-Flags, int enum marked with FSMConditionAttribute.",
                    nameof(conditionType));

            string nextTypeID = FSMConditionType.GetID(conditionType);
            if (this.conditionTypeID == nextTypeID)
                return;

            this.conditionTypeID = nextTypeID;
            for (int i = 0; i < this.transitions.Count; i++)
                this.transitions[i]?.ClearCondition();
        }

        /// <summary>
        /// 에디터에서 상태 ID 이름으로 사용할 enum 타입 지정
        /// </summary>
        public void SetStateIDType(Type stateIDType)
        {
            if (stateIDType != null && !FSMStateIDType.IsValid(stateIDType))
                throw new ArgumentException(
                    "State ID type must be a unique, non-Flags, int enum marked with FSMStateIDAttribute.",
                    nameof(stateIDType));

            if (stateIDType != null)
            {
                for (int i = 0; i < this.states.Count; i++)
                {
                    if (!Enum.IsDefined(stateIDType, this.states[i].ID))
                        throw new InvalidOperationException(
                            $"State ID {this.states[i].ID} is not defined in {stateIDType.FullName}.");
                }
            }

            this.stateIDTypeID = FSMStateIDType.GetID(stateIDType);
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
#endif

        private bool ContainsState(int stateID)
        {
            for (int i = 0; i < this.states.Count; i++)
            {
                FSMStateData state = this.states[i];
                if (state != null && state.ID == stateID)
                    return true;
            }

            return false;
        }

    }
}
