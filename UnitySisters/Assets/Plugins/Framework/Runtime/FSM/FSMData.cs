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
    public sealed class FSMTransitionData : ISerializationCallbackReceiver
    {
        [SerializeField] private int fromStateID;
        [SerializeField] private int toStateID;
        [SerializeField] private string name;
        [SerializeField] private FSMTransitionMode mode;
        [SerializeField] private List<FSMConditionData> conditions = new List<FSMConditionData>();
        [SerializeField] private int priority;
        [FormerlySerializedAs("hasCondition")]
        [SerializeField, HideInInspector] private bool legacyHasCondition;
        [FormerlySerializedAs("conditionValue")]
        [FormerlySerializedAs("conditionID")]
        [SerializeField, HideInInspector] private int legacyConditionID;
#if UNITY_EDITOR
        [SerializeField] private List<Vector2> routePoints = new List<Vector2>();
#endif

        public int FromStateID => this.fromStateID;
        public int ToStateID => this.toStateID;
        public string Name => this.name;
        public bool HasCondition => this.conditions != null && this.conditions.Count > 0;
        public int ConditionID => GetFirstCustomConditionID();
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

        public FSMTransitionMode GetMode() => this.mode;

        public IReadOnlyList<FSMConditionData> GetConditions()
        {
            return this.conditions ?? (IReadOnlyList<FSMConditionData>)Array.Empty<FSMConditionData>();
        }

        public void SetMode(FSMTransitionMode mode)
        {
            this.mode = mode;
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
            EnsureConditions();
            this.conditions.Clear();
            AddCustomCondition(conditionID);
        }

        /// <summary>
        /// 전이에 지정된 조건 제거
        /// </summary>
        public void ClearCondition()
        {
            this.conditions?.Clear();
        }

        /// <summary>
        /// 지정한 Parameter 값을 비교하는 조건 추가
        /// </summary>
        public FSMConditionData AddParameterCondition(int parameterID)
        {
            EnsureConditions();
            var condition = new FSMConditionData(FSMConditionKind.Parameter);
            condition.SetParameter(parameterID);
            this.conditions.Add(condition);
            return condition;
        }

        /// <summary>
        /// 게임 코드의 조건 팩토리가 해석할 Custom Condition ID 추가
        /// </summary>
        public FSMConditionData AddCustomCondition(int conditionID)
        {
            EnsureConditions();
            var condition = new FSMConditionData(FSMConditionKind.Custom);
            condition.SetCustomCondition(conditionID);
            this.conditions.Add(condition);
            return condition;
        }

        public bool RemoveCondition(FSMConditionData condition)
        {
            return condition != null && this.conditions != null && this.conditions.Remove(condition);
        }

        public void ClearCustomConditions()
        {
            if (this.conditions == null)
                return;

            for (int i = this.conditions.Count - 1; i >= 0; i--)
            {
                FSMConditionData condition = this.conditions[i];
                if (condition != null && condition.GetConditionKind() == FSMConditionKind.Custom)
                    this.conditions.RemoveAt(i);
            }
        }

        public void RemoveParameterConditions(int parameterID)
        {
            if (this.conditions == null)
                return;

            for (int i = this.conditions.Count - 1; i >= 0; i--)
            {
                FSMConditionData condition = this.conditions[i];
                if (condition != null &&
                    condition.GetConditionKind() == FSMConditionKind.Parameter &&
                    condition.GetParameterID() == parameterID)
                    this.conditions.RemoveAt(i);
            }
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

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            EnsureConditions();
            // 이전 단일 Condition ID를 새 복수 조건 구조의 Custom Condition 하나로 이관한다.
            if (this.legacyHasCondition && this.conditions.Count == 0)
                AddCustomCondition(this.legacyConditionID);

            this.legacyHasCondition = false;
            this.legacyConditionID = 0;
        }

        private int GetFirstCustomConditionID()
        {
            if (this.conditions == null)
                return 0;

            for (int i = 0; i < this.conditions.Count; i++)
            {
                FSMConditionData condition = this.conditions[i];
                if (condition != null && condition.GetConditionKind() == FSMConditionKind.Custom)
                    return condition.GetCustomConditionID();
            }

            return 0;
        }

        private void EnsureConditions()
        {
            if (this.conditions == null)
                this.conditions = new List<FSMConditionData>();
        }
    }

    [CreateAssetMenu(fileName = "FSMData", menuName = "FSM/State Machine")]
    public class FSMData : ScriptableObject
    {
        [SerializeField] private int initialStateID;
        [SerializeField] private bool useFieldParameterBinding;
#if UNITY_EDITOR
        [SerializeField] private int nextStateID;
        [SerializeField] private int nextParameterID;
        [SerializeField] private string stateIDTypeID;
        [SerializeField] private string conditionTypeID;
        [SerializeField] private string parameterSourceTypeID;
#endif
        [SerializeField] private List<FSMStateData> states = new List<FSMStateData>();
        [SerializeField] private List<FSMTransitionData> transitions = new List<FSMTransitionData>();
        [SerializeField] private List<FSMParameterData> parameters = new List<FSMParameterData>();

        public int InitialStateID => this.initialStateID;
        public bool GetUsesFieldParameterBinding() => this.useFieldParameterBinding;
#if UNITY_EDITOR
        public string StateIDTypeID => this.stateIDTypeID;
        public string ConditionTypeID => this.conditionTypeID;
        public string GetParameterSourceTypeID() => this.parameterSourceTypeID;
#endif
        public IReadOnlyList<FSMStateData> States => this.states;
        public IReadOnlyList<FSMTransitionData> Transitions => this.transitions;

        public IReadOnlyList<FSMParameterData> GetParameters()
        {
            EnsureParameters();
            return this.parameters;
        }

#if UNITY_EDITOR
        /// <summary>
        /// 에디터에서 새 Parameter를 추가하고 재사용하지 않는 ID 발급
        /// </summary>
        public FSMParameterData AddParameter(string name, FSMParameterType type)
        {
            EnsureParameters();
            int parameterID = GetNextParameterID();
            var parameter = new FSMParameterData(parameterID, name, type);
            this.parameters.Add(parameter);
            return parameter;
        }

        /// <summary>
        /// enum 등 외부 계약에서 정한 ID로 Parameter 추가
        /// </summary>
        public FSMParameterData AddParameter(int parameterID, string name, FSMParameterType type)
        {
            EnsureParameters();
            if (FindParameterRuntime(parameterID) != null)
                throw new ArgumentException(
                    $"Parameter ID {parameterID} already exists.",
                    nameof(parameterID));

            var parameter = new FSMParameterData(parameterID, name, type);
            this.parameters.Add(parameter);
            return parameter;
        }

        /// <summary>
        /// Parameter와 이를 참조하는 모든 전이 조건을 함께 제거
        /// </summary>
        public bool RemoveParameter(FSMParameterData parameter)
        {
            EnsureParameters();
            if (parameter == null || !this.parameters.Remove(parameter))
                return false;

            int parameterID = parameter.GetID();
            for (int i = 0; i < this.transitions.Count; i++)
                this.transitions[i]?.RemoveParameterConditions(parameterID);

            return true;
        }

        public FSMParameterData FindParameter(int parameterID)
        {
            for (int i = 0; i < this.parameters.Count; i++)
            {
                FSMParameterData parameter = this.parameters[i];
                if (parameter != null && parameter.GetID() == parameterID)
                    return parameter;
            }

            return null;
        }

        public FSMParameterData FindBoundParameter(string bindingKey)
        {
            if (string.IsNullOrEmpty(bindingKey))
                return null;

            for (int i = 0; i < this.parameters.Count; i++)
            {
                FSMParameterData parameter = this.parameters[i];
                if (parameter != null && parameter.GetBindingKey() == bindingKey)
                    return parameter;
            }

            return null;
        }

        public int GetBoundParameterCount()
        {
            int count = 0;
            for (int i = 0; i < this.parameters.Count; i++)
            {
                if (this.parameters[i] != null && this.parameters[i].GetIsFieldBound())
                    count++;
            }
            return count;
        }

        public void SetParameterSourceTypeID(string typeID)
        {
            this.parameterSourceTypeID = typeID ?? string.Empty;
            this.useFieldParameterBinding = !string.IsNullOrEmpty(this.parameterSourceTypeID);
        }

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
            EnsureParameters();
            var stateMachine = new StateMachine(owner, this, this.parameters);
            Dictionary<int, Func<IStateMachine, bool>> conditionCache = conditionFactory != null
                ? new Dictionary<int, Func<IStateMachine, bool>>()
                : null;

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
                IReadOnlyList<FSMConditionData> conditionDataList = transitionData.GetConditions();
                StateTransitionCondition[] runtimeConditions = conditionDataList.Count > 0
                    ? new StateTransitionCondition[conditionDataList.Count]
                    : null;
                for (int conditionIndex = 0; conditionIndex < conditionDataList.Count; conditionIndex++)
                {
                    FSMConditionData conditionData = conditionDataList[conditionIndex];
                    if (conditionData == null)
                        throw new InvalidOperationException(
                            $"Transition '{transitionData.Name}' contains a null condition.");

                    runtimeConditions[conditionIndex] = CreateRuntimeCondition(
                        transitionData,
                        conditionData,
                        conditionFactory,
                        conditionCache);
                }

                stateMachine.AddTransition(new StateTransition(
                    transitionData.FromStateID,
                    transitionData.ToStateID,
                    runtimeConditions,
                    transitionData.GetMode(),
                    transitionData.Priority,
                    transitionData.Name));
            }

            return stateMachine;
        }

        /// <summary>
        /// 직렬화 조건을 할당 없는 런타임 평가 조건으로 변환하고 Custom delegate를 ID별로 재사용한다.
        /// </summary>
        private StateTransitionCondition CreateRuntimeCondition(
            FSMTransitionData transitionData,
            FSMConditionData conditionData,
            Func<int, Func<IStateMachine, bool>> conditionFactory,
            Dictionary<int, Func<IStateMachine, bool>> conditionCache)
        {
            if (conditionData.GetConditionKind() == FSMConditionKind.Parameter)
            {
                int parameterIndex = FindParameterIndexRuntime(conditionData.GetParameterID());
                if (parameterIndex < 0)
                    throw new InvalidOperationException(
                        $"Transition '{transitionData.Name}' references unknown parameter ID " +
                        $"{conditionData.GetParameterID()}.");

                return StateTransitionCondition.CreateParameter(
                    conditionData,
                    this.parameters[parameterIndex].GetParameterType(),
                    parameterIndex);
            }

            if (conditionFactory == null)
                throw new InvalidOperationException(
                    $"Transition '{transitionData.Name}' requires a custom condition, " +
                    "but no condition factory was provided.");

            int conditionID = conditionData.GetCustomConditionID();
            if (!conditionCache.TryGetValue(conditionID, out Func<IStateMachine, bool> condition))
            {
                condition = conditionFactory.Invoke(conditionID);
                if (condition == null)
                    throw new InvalidOperationException(
                        $"Condition factory returned null for transition '{transitionData.Name}'.");

                conditionCache.Add(conditionID, condition);
            }

            return StateTransitionCondition.CreateCustom(
                condition,
                conditionData.GetCustomExpectedResult());
        }

        /// <summary>
        /// 실행 전에 중복 ID, 시작 상태와 끊어진 전이를 검사
        /// </summary>
        public void ValidateDefinition()
        {
            EnsureParameters();
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

            for (int i = 0; i < this.parameters.Count; i++)
            {
                FSMParameterData parameter = this.parameters[i];
                if (parameter == null)
                    throw new InvalidOperationException(
                        $"FSMData '{this.name}' contains a null parameter.");

                for (int duplicateIndex = 0; duplicateIndex < i; duplicateIndex++)
                {
                    if (this.parameters[duplicateIndex].GetID() == parameter.GetID())
                        throw new InvalidOperationException(
                            $"FSMData '{this.name}' contains duplicate parameter ID " +
                            $"{parameter.GetID()}.");
                }
            }

            for (int i = 0; i < this.transitions.Count; i++)
            {
                FSMTransitionData transition = this.transitions[i];
                if (transition == null)
                    throw new InvalidOperationException($"FSMData '{this.name}' contains a null transition.");
                if (!ContainsState(transition.FromStateID) || !ContainsState(transition.ToStateID))
                    throw new InvalidOperationException(
                        $"Transition '{transition.Name}' in FSMData '{this.name}' references an unknown state.");

                IReadOnlyList<FSMConditionData> transitionConditions = transition.GetConditions();
                for (int conditionIndex = 0; conditionIndex < transitionConditions.Count; conditionIndex++)
                {
                    FSMConditionData condition = transitionConditions[conditionIndex];
                    if (condition == null)
                        throw new InvalidOperationException(
                            $"Transition '{transition.Name}' in FSMData '{this.name}' contains a null condition.");
                    if (condition.GetConditionKind() == FSMConditionKind.Parameter &&
                        FindParameterRuntime(condition.GetParameterID()) == null)
                        throw new InvalidOperationException(
                            $"Transition '{transition.Name}' references unknown parameter ID " +
                            $"{condition.GetParameterID()}.");
                }
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
                this.transitions[i]?.ClearCustomConditions();
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

        private int GetNextParameterID()
        {
            while (FindParameterRuntime(this.nextParameterID) != null)
                this.nextParameterID++;

            return this.nextParameterID++;
        }
#endif

        private FSMParameterData FindParameterRuntime(int parameterID)
        {
            int parameterIndex = FindParameterIndexRuntime(parameterID);
            return parameterIndex >= 0 ? this.parameters[parameterIndex] : null;
        }

        private int FindParameterIndexRuntime(int parameterID)
        {
            EnsureParameters();
            for (int i = 0; i < this.parameters.Count; i++)
            {
                FSMParameterData parameter = this.parameters[i];
                if (parameter != null && parameter.GetID() == parameterID)
                    return i;
            }

            return -1;
        }

        private void EnsureParameters()
        {
            if (this.parameters == null)
                this.parameters = new List<FSMParameterData>();
        }

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
